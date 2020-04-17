using Server.Regions;
using System;

namespace Server.Items
{
    public class MushroomTrap : BaseTrap
    {
        [Constructable]
        public MushroomTrap()
            : base(0x1125)
        {
        }

        public MushroomTrap(Serial serial)
            : base(serial)
        {
        }

        public override bool PassivelyTriggered => true;
        public override TimeSpan PassiveTriggerDelay => TimeSpan.Zero;
        public override int PassiveTriggerRange => 2;
        public override TimeSpan ResetDelay => TimeSpan.Zero;
        public override void OnTrigger(Mobile from)
        {
            if (!from.Alive || this.ItemID != 0x1125 || from.IsStaff())
                return;

            this.ItemID = 0x1126;
            Effects.PlaySound(this.Location, this.Map, 0x306);

            Spells.SpellHelper.Damage(TimeSpan.FromSeconds(0.5), from, from, Utility.Dice(2, 4, 0));

            Timer.DelayCall(TimeSpan.FromSeconds(2.0), new TimerCallback(OnMushroomReset));
        }

        public virtual void OnMushroomReset()
        {
            if (Region.Find(this.Location, this.Map).IsPartOf<DungeonRegion>())
                this.ItemID = 0x1125; // reset
            else
                this.Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();

            if (this.ItemID == 0x1126)
                this.OnMushroomReset();
        }
    }
}