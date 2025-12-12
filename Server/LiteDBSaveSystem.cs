using LiteDB;
using Server.Guilds;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Server
{
    public static class LiteDBSaveSystem
    {
        public static readonly LiteDatabase _db;
        private static readonly ILiteCollection<DBRecord> _mobileCol;
        private static readonly ILiteCollection<DBRecord> _itemCol;
        private static readonly ILiteCollection<BaseGuild> _guildCol;
        public static readonly ILiteCollection<AccountRecord> _accCol;

        static LiteDBSaveSystem()
        {
            string folder = "Saves/Database";
            Directory.CreateDirectory(folder);
            _db = new LiteDatabase(Path.Combine(folder, "world.db"));

            _mobileCol = _db.GetCollection<DBRecord>("mobiles");
            _itemCol = _db.GetCollection<DBRecord>("items");
            _guildCol = _db.GetCollection<BaseGuild>("guilds");
            _accCol = _db.GetCollection<AccountRecord>("accounts");

            _mobileCol.EnsureIndex(x => x.Serial, true);
            _itemCol.EnsureIndex(x => x.Serial, true);
            _guildCol.EnsureIndex(x => x.Id, true);            
            _accCol.EnsureIndex(x => x.Username, true);
        }

        private static Mobile[] mobarray = null;
        private static Item[] itemarray = null;

        static int asd;
        public static void Save(bool message)
        {
            long totalsw = Stopwatch.GetTimestamp();
            EventSink.InvokeBeforeWorldSave(new BeforeWorldSaveEventArgs());

            long totalsw2 = Stopwatch.GetTimestamp();
            long totalsw3 = Stopwatch.GetTimestamp();
            long totalsw4 = Stopwatch.GetTimestamp();

            mobarray = World.Mobiles.Values.ToArray();
            itemarray = World.Items.Values.ToArray();

            long sw = Stopwatch.GetTimestamp();
            Parallel.ForEach(mobarray, m => {
                m.CheckDirtyFlag();
            });

            Parallel.ForEach(itemarray, i => {
                if (i.Decays && i.Parent == null && i.Map != Map.Internal && (i.LastMoved + i.DecayTime) <= DateTime.UtcNow)
                {
                    i.Delete();
                }
                i.CheckDirtyFlag();
            });
            Console.WriteLine($"Dirtycheck took {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw)}");
            sw = Stopwatch.GetTimestamp();
            var dirtyMobiles = mobarray.Where(m => m.Dirty).ToArray();
            var dirtyItems = itemarray.Where(i => i.Dirty).ToArray();
            Console.WriteLine($"Get dirt Linq took {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw)}");


            _db.BeginTrans();

            try
            {
                Write(_mobileCol, dirtyMobiles, asd);
                Write(_itemCol, dirtyItems, asd);
                Write(BaseGuild.List);
                totalsw2 = Stopwatch.GetTimestamp() - totalsw2;
                totalsw3 = Stopwatch.GetTimestamp();
                EventSink.InvokeWorldSave(new WorldSaveEventArgs(message));
                totalsw3 = Stopwatch.GetTimestamp() - totalsw3;
                totalsw4 = Stopwatch.GetTimestamp();
                asd = (asd+1) % 4;

                _db.Commit();
            }
            catch
            {
                World.Broadcast(0, false, "Error while writing to DB! Rollback!");
                Console.WriteLine("Error while writing to DB! Rollback!");
                _db.Rollback();
            }
            Console.WriteLine($"Total Savetime without save eventhandlers! {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - totalsw4 + totalsw2 - totalsw3 )}");
            EventSink.InvokeAfterWorldSave(new AfterWorldSaveEventArgs());
            Console.WriteLine($"Total Savetime with pre, mid and post save eventhandlers! {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - totalsw)}\n");
        }

        private static void Write<T>(ILiteCollection<DBRecord> dbcol, T[] collection, int asd) where T : IEntity, ISerializable
        {
            long sw = Stopwatch.GetTimestamp();
            long gcc = GC.GetTotalMemory(false);

            if (asd == 0) // just a switch to ping pong between the 2 "styles" for testing
            {
                foreach (var dirt in collection)
                {
                    if (dirt.Deleted)
                    {
                        dbcol.Delete(dirt.Serial.Value);
                    }
                    else
                    {
                        //Console.WriteLine(_db.Mapper.ToDocument(rec).ToString());
                        dbcol.Upsert(ToRecord(dirt));
                        dirt.ClearDirty();
                    }
                }
                Console.WriteLine($"Write for collection Mode 1 {collection.GetType()} took {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw)}");
            }
            else if(asd == 1)// >10% faster, but more garbage? hmm..
            {
                var dirty = collection.Where(x => x.Deleted).Select(x => x.Serial.Value).ToHashSet();
                var toupdate = collection
                .Where(x => !x.Deleted && x.Dirty)
                .AsParallel()
                .Select(x => { x.ClearDirty(); return ToRecord(x); })
                .ToList();

                if (dirty.Count > 0)
                    dbcol.DeleteMany(x => dirty.Contains(x.Serial));
                dbcol.Upsert(toupdate);
                Console.WriteLine($"Write for collection Mode 2 {collection.GetType()} took {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw)}");
            }
            else if(asd == 2)
            {
                int count = 0;
                int dirtyCount = 0;
                foreach (var x in collection)
                {
                    if (x.Deleted) count++;
                    else if (x.Dirty) dirtyCount++;
                }

                // 2. Vorallokierte Arrays nutzen, um temporären GC zu vermeiden
                int[] toDelete = new int[count];
                DBRecord[] toUpdate = new DBRecord[dirtyCount];

                int deleteIndex = 0;
                int updateIndex = 0;

                foreach (var x in collection)
                {
                    if (x.Deleted)
                        toDelete[deleteIndex++] = x.Serial.Value;
                    else if (x.Dirty)
                    {
                        // DBRecord erstellen, dirty flag zurücksetzen
                        toUpdate[updateIndex++] = ToRecord(x);
                        x.ClearDirty();
                    }
                }

                // 3. Löschen und Upsert
                if (toDelete.Length > 0)
                    dbcol.DeleteMany(x => toDelete.Contains(x.Serial));

                if (toUpdate.Length > 0)
                    dbcol.Upsert(toUpdate);

                Console.WriteLine($"Write optimized {collection.GetType()} took {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw)}");
            }
            else
            {
                var toDelete = new HashSet<int>((collection as ICollection<T>).Count);
                var toUpdate = new List<DBRecord>((collection as ICollection<T>).Count);

                foreach (var item in collection)
                {
                    if (item.Deleted)
                    {
                        toDelete.Add(item.Serial.Value);
                        continue;
                    }
                    toUpdate.Add(ToRecord(item));
                    item.ClearDirty();
                }

                if (toDelete.Count > 0)
                    dbcol.DeleteMany(x => toDelete.Contains(x.Serial));

                if (toUpdate.Count > 0)
                    dbcol.Upsert(toUpdate);

                Console.WriteLine($"Write optimized 2? {collection.GetType()} took {TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw)}");
            }
            Console.WriteLine($"Write caused {GC.GetTotalMemory(false) - gcc} garbagebytes");
        }

        public static void Write(IEnumerable<AccountRecord> collection)
        {
            _accCol.Upsert(collection);
        }
        private static void Write(Dictionary<int, BaseGuild> list)
        {
            _guildCol.Upsert(list.Values);
        }

        public static void Load()
        {
            Load(World.Mobiles, _mobileCol);
            Load(World.Items, _itemCol);
            Load(_guildCol);

            Deserialize();

            EventSink.InvokeWorldLoad();
        }

        private static void Load(ILiteCollection<BaseGuild> guildCol)
        {
            foreach (var rec in guildCol.FindAll())
            {
               
            }
        }

        private static void Load<T>(Dictionary<Serial, T> col, ILiteCollection<DBRecord> dbcol) where T : IEntity, ISerializable
        {
            foreach (var rec in dbcol.FindAll())
            {
                T entity = FromRecord<T>(rec);
                if (entity != null)
                {
                    col[entity.Serial] = entity;
                    toDeserialize.Enqueue(new EntityData() { Entity = entity, Data = rec.Data });
                }
            }
        }

        private struct EntityData
        {
            public IEntity Entity;
            public byte[] Data;
        }

        private static Queue<EntityData> toDeserialize = new Queue<EntityData>();
        private static BinaryReader _br;
        private static BinaryFileReader _reader;
        private static void Deserialize()
        {
            foreach (var entry in toDeserialize)
            {
                var obj = entry.Entity;
                var data = entry.Data;

                if (_ms == null)
                    _ms = new MemoryStream(4096);
                else
                {
                    _ms.SetLength(0);
                    if (_ms.Capacity < data.Length)
                        _ms.Capacity = data.Length;
                }

                _ms.Write(data, 0, data.Length);
                _ms.Position = 0;

                if (_br == null)
                    _br = new BinaryReader(_ms, System.Text.Encoding.Default, true);

                if(_reader  == null)
                    _reader = new BinaryFileReader(_br);

                if (obj is Item i)
                    i.Deserialize(_reader);
                else if (obj is Mobile m)
                    m.Deserialize(_reader);
            }
            _br.Close();
            _ms.Close();
            _ms.Dispose();
            _reader = null;
            _br = null;
            _ms = null;
        }

        private static readonly Type[] SerialCtorParam = { typeof(Serial) };

        private static T FromRecord<T>(DBRecord rec) where T : IEntity
        {
            if (rec?.Data == null)
                return default(T);
            T obj;

            Type type = ScriptCompiler.FindTypeByFullName(rec.EntityType);

            if (type == null)
                throw new Exception($"Ungültiger type {type} für {typeof(T).Name}");

            if (!typeof(T).IsAssignableFrom(type))
                throw new Exception($"Geladener Typ {type} ist kein {typeof(T).Name}");

            ConstructorInfo ctor = type.GetConstructor(SerialCtorParam);
            obj = (T)(ctor.Invoke(new object[] { (Serial)rec.Serial }));

            return obj;
        }

        [ThreadStatic]
        private static MemoryStream _ms;
        [ThreadStatic]
        private static BinaryFileWriter _writer;
        private static DBRecord ToRecord<T>(T dirt) where T : IEntity, ISerializable
        {
            if (_ms == null)
                _ms = new MemoryStream(4096);

            _ms.Position = 0;
            _ms.SetLength(0);

            if (_writer == null)
                _writer = new BinaryFileWriter(_ms, true);

            dirt.Serialize(_writer);
            _writer.Flush();

            int length = (int)_ms.Position; // tatsächliche Datenlänge
            byte[] data = new byte[length];
            Array.Copy(_ms.GetBuffer(), 0, data, 0, length);

            return new DBRecord
            {
                Serial = dirt.Serial,
                EntityType = dirt.GetType().FullName,
                Data = data
            };
        }

        public class DBRecord
        {
            [BsonId]
            public int Serial { get; set; }
            public string EntityType { get; set; }
            public byte[] Data { get; set; }
        }
        public class AccountRecord
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string PlainPassword { get; set; }
            public string MD5Password { get; set; }
            public string SHA1Password { get; set; }
            public string SHA512Password { get; set; }
            public AccessLevel AccessLevel { get; set; }
            public int Flags { get; set; }
            public DateTime Created { get; set; }
            public DateTime LastLogin { get; set; }
            public TimeSpan TotalGameTime { get; set; }
            public IndexValuePair[] MobileEntries { get; set; }
            public AccountCommentRecord[] AccountComments { get; set; }
            public IPAddress[] LoginIPs { get; set; }
            public string[] IPRestrictions { get; set; }
            public double TotalCurrency { get; set; }
            public int Sovereigns { get; set; }
            public IndexValuePair[] MobileSecureAccountBalances { get; set; }
            public StringPair[] Tags { get; set; }
        }

        public class IndexValuePair
        {
            public int Index { get; set; }
            public int Value { set; get; }

            public IndexValuePair() { }
            public IndexValuePair(int i, int serial)
            {
                Index = i;
                Value = serial;
            }
        }
        public class StringPair
        {
            public string Item1 { get; set; }
            public string Item2 { set; get; }

            public StringPair() { }
            public StringPair(string item1, string item2)
            {
                Item1 = item1;
                Item2 = item2;
            }
        }
        public class AccountCommentRecord
        {
            public string AddedBy { set; get; }
            public string Content { set; get; }
            public DateTime LastModified { set; get; }
            public AccountCommentRecord() { }
        }

        public static bool CheckDirty(IEntity ent)
        {
            if (ent == null || ent.Deleted || ent.Dirty)
                return true;

            ulong newHash = SnapshotEngine.ComputeSnapshotHash(ent as ISerializable);

            if (!ent.SnapshotHash.Equals(newHash))
            {
                ent.SnapshotHash = newHash;
                ent.Dirty = true;
                return true;
            }

            return false;
        }
    }

    public static class XXHash64
    {
        private const ulong PRIME64_1 = 11400714785074694791UL;
        private const ulong PRIME64_2 = 14029467366897019727UL;
        private const ulong PRIME64_3 = 1609587929392839161UL;
        private const ulong PRIME64_4 = 9650029242287828579UL;
        private const ulong PRIME64_5 = 2870177450012600261UL;

        public static ulong Hash(byte[] data, int len, ulong seed = 0)
        {
            int index = 0;
            ulong hash;

            if (len >= 32)
            {
                ulong v1 = seed + PRIME64_1 + PRIME64_2;
                ulong v2 = seed + PRIME64_2;
                ulong v3 = seed + 0;
                ulong v4 = seed - PRIME64_1;

                int limit = len - 32;
                while (index <= limit)
                {
                    v1 = Round(v1, BitConverter.ToUInt64(data, index)); index += 8;
                    v2 = Round(v2, BitConverter.ToUInt64(data, index)); index += 8;
                    v3 = Round(v3, BitConverter.ToUInt64(data, index)); index += 8;
                    v4 = Round(v4, BitConverter.ToUInt64(data, index)); index += 8;
                }

                hash =
                    Rotl(v1, 1) +
                    Rotl(v2, 7) +
                    Rotl(v3, 12) +
                    Rotl(v4, 18);
            }
            else
            {
                hash = seed + PRIME64_5;
            }

            hash += (ulong)len;

            while (index + 8 <= len)
            {
                ulong k1 = BitConverter.ToUInt64(data, index);
                k1 *= PRIME64_2;
                k1 = Rotl(k1, 31);
                k1 *= PRIME64_1;
                hash ^= k1;

                hash = Rotl(hash, 27) * PRIME64_1 + PRIME64_4;
                index += 8;
            }

            while (index < len)
            {
                hash ^= (ulong)data[index] * PRIME64_5;
                hash = Rotl(hash, 11) * PRIME64_1;
                index++;
            }

            hash ^= hash >> 33;
            hash *= PRIME64_2;
            hash ^= hash >> 29;
            hash *= PRIME64_3;
            hash ^= hash >> 32;

            return hash;
        }

        private static ulong Round(ulong acc, ulong input)
        {
            acc += input * PRIME64_2;
            acc = Rotl(acc, 31);
            acc *= PRIME64_1;
            return acc;
        }

        private static ulong Rotl(ulong x, int r)
        {
            return (x << r) | (x >> (64 - r));
        }
    }

    public static class SnapshotBufferPool
    {
        [ThreadStatic]
        private static byte[] _buffer;

        public static byte[] Rent(int minSize)
        {
            if (_buffer == null || _buffer.Length < minSize)
            {
                // nächstgrößere Zweierpotenz, damit wir nicht dauernd wachsen
                int size = 1;
                while (size < minSize) size <<= 1;
                _buffer = new byte[size];
            }

            return _buffer;
        }
    }

    public static class SnapshotEngine
    {
        [ThreadStatic]
        private static MemoryStream _ms;
        [ThreadStatic]
        private static BinaryFileWriter writer;

        public static ulong ComputeSnapshotHash(ISerializable ent)
        {
            if (_ms == null)
                _ms = new MemoryStream(4096); // initial pool size

            _ms.Position = 0;   // reset
            _ms.SetLength(0);   // truncate (no alloc)

            if (writer == null)
                writer = new BinaryFileWriter(_ms, true);

            ent.Serialize(writer);
            writer.Flush();

            int len = (int)_ms.Length;
            byte[] buffer = SnapshotBufferPool.Rent(len);
            Buffer.BlockCopy(_ms.GetBuffer(), 0, buffer, 0, len);

            return XXHash64.Hash(buffer, len);
        }
    }
}
