namespace Server.Items
{
    public class TableWithRedClothAddon : BaseAddon
    {
        [Constructable]
        public TableWithRedClothAddon()
            : base()
        {
            AddComponent(new LocalizedAddonComponent(0x118D, 1076277), 0, 0, 0);
        }

        public TableWithRedClothAddon(Serial serial)
            : base(serial)
        {
        }

        public override BaseAddonDeed Deed => new TableWithRedClothDeed();
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.WriteEncodedInt(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadEncodedInt();
        }
    }

    public class TableWithRedClothDeed : BaseAddonDeed
    {
        [Constructable]
        public TableWithRedClothDeed()
            : base()
        {
            LootType = LootType.Blessed;
        }

        public TableWithRedClothDeed(Serial serial)
            : base(serial)
        {
        }

        public override BaseAddon Addon => new TableWithRedClothAddon();
        public override int LabelNumber => 1076277;// Table With A Red Tablecloth
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.WriteEncodedInt(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadEncodedInt();
        }
    }
}