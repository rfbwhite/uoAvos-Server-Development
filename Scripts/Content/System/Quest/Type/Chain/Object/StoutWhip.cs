﻿namespace Server.Engines.ChainQuests.Items
{
	public class StoutWhip : Item
	{
		public override int LabelNumber => 1074812;  // Stout Whip

		[Constructable]
		public StoutWhip() : base(0x166F)
		{
			LootType = LootType.Blessed;
		}

		public StoutWhip(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0); // Version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
		}
	}
}