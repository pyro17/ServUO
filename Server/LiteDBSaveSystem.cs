using LiteDB;
using Server.Accounting;
using Server.Guilds;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        }

        public static void Save(bool message)
        {

            EventSink.InvokeBeforeWorldSave(new BeforeWorldSaveEventArgs());
            var mobarray = World.Mobiles.Values.ToArray();
            var itemarray = World.Items.Values.ToArray();

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

            var dirtyMobiles = mobarray.Where(m => m.Dirty);
            var dirtyItems = itemarray.Where(i => i.Dirty);


            _db.BeginTrans();

            try
            {
                Write(_mobileCol, dirtyMobiles);
                Write(_itemCol, dirtyItems);
                Write(BaseGuild.List);
                EventSink.InvokeWorldSave(new WorldSaveEventArgs(message));

                _db.Commit();
            }
            catch(Exception)
            {
                World.Broadcast(0, false, "Error while writing to DB! Rollback!");
                Console.WriteLine("Error while writing to DB! Rollback!");
                _db.Rollback();
            }
            EventSink.InvokeAfterWorldSave(new AfterWorldSaveEventArgs());
        }

        private static void Write<T>(ILiteCollection<DBRecord> dbcol, IEnumerable<T> collection) where T : IEntity, ISerializable
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
                }

                dirt.ClearDirty();
            }
        }

        public static void Write(IEnumerable<AccountRecord> collection)
        {
          /*  foreach (var account in collection)
            {
                Console.WriteLine(_db.Mapper.ToDocument(account).ToString());
            }*/
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
                    toDeserialze.Enqueue((entity, rec.Data));
                }
            }
        }

        public static void LoadAccounts(Dictionary<string, IAccount> accounts)
        {
        }

        private static Queue<(IEntity, byte[])> toDeserialze = new Queue<(IEntity, byte[])>();
        private static void Deserialize()
        {
            foreach (var entity in toDeserialze)
            {
                var obj = entity.Item1;
                using (var ms = new MemoryStream(entity.Item2))
                using (var br = new BinaryReader(ms))
                {
                    var reader = new BinaryFileReader(br);
                    if (obj is Item i)
                        i.Deserialize(reader);
                    else if (obj is Mobile m)
                        m.Deserialize(reader);
                }
            }
        }

        private static T FromRecord<T>(DBRecord rec) where T : IEntity
        {
            if (rec?.Data == null)
                return default(T);
            T obj;

            string typename = rec.EntityType;
            Type type = ScriptCompiler.FindTypeByFullName(typename);

            if (type == null)
                throw new Exception($"Ungültiger type {type} für {typeof(T).Name}");

            if (!typeof(T).IsAssignableFrom(type))
                throw new Exception($"Geladener Typ {type} ist kein {typeof(T).Name}");

            ConstructorInfo ctor = type.GetConstructor(new Type[] { typeof(Serial)});
            obj = (T)(ctor.Invoke(new object[] { (Serial)rec.Serial }));

            return obj;
        }

        private static DBRecord ToRecord<T>(T dirt) where T : IEntity, ISerializable
        {
            using (var ms = new MemoryStream())
            {
                var writer = new BinaryFileWriter(ms, true);
                dirt.Serialize(writer);
                writer.Flush();

                var rec = new DBRecord
                {
                    Serial = dirt.Serial,
                    EntityType = dirt.GetType().FullName,
                    Data = ms.ToArray()
                };
                return rec;
            }
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

        public static void TakeSnapshot(IEntity dbentitity, PropertyInfo[] props)
        {
            if (dbentitity == null || dbentitity.Deleted) return;

            foreach (var prop in props)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                dbentitity.PropertySnapshot[prop.Name] = prop.GetValue(dbentitity);
            }
        }

        public static bool CheckDirty(IEntity dbentitity)
        {
            if (dbentitity == null || dbentitity.Dirty || dbentitity.Deleted) return true;

            bool returnval = false;
            var props = dbentitity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                if (!dbentitity.PropertySnapshot.TryGetValue(prop.Name, out var oldValue)) returnval = true;

                if (!returnval && !Equals(oldValue, prop.GetValue(dbentitity))) returnval = true;

                if (returnval)
                {
                    TakeSnapshot(dbentitity, props);
                    break;
                }
            }
            return returnval;
        }
    }
}
