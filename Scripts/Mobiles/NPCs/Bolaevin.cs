using Server.Items;
using System.Collections.Generic;

namespace Server.Mobiles
{
    public class Bolaevin : BaseVendor
    {
        private readonly List<SBInfo> m_SBInfos = new List<SBInfo>();
        [Constructable]
        public Bolaevin()
            : base("the arcanist")
        {
            this.Name = "Bolaevin";
        }

        public Bolaevin(Serial serial)
            : base(serial)
        {
        }

        public override bool CanTeach => false;
        public override bool IsInvulnerable => true;
        protected override List<SBInfo> SBInfos => this.m_SBInfos;
        public override void InitSBInfo()
        {
        }

        public override void InitBody()
        {
            this.InitStats(100, 100, 25);

            this.Female = false;
            this.Race = Race.Elf;

            this.Hue = 0x84DE;
            this.HairItemID = 0x2FC0;
            this.HairHue = 0x36;
        }

        public override void InitOutfit()
        {
            this.AddItem(new ElvenBoots(0x3B3));
            this.AddItem(new RoyalCirclet());
            this.AddItem(new LeafChest());
            this.AddItem(new LeafArms());

            Item item;

            item = new LeafLegs();
            item.Hue = 0x1BB;
            this.AddItem(item);
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
        }
    }
}