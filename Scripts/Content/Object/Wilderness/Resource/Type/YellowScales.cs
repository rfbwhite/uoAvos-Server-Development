﻿namespace Server.Items
{
	public class YellowScales : BaseScales
	{
		[Constructable]
		public YellowScales() : this(1)
		{
		}

		[Constructable]
		public YellowScales(int amount) : base(CraftResource.YellowScales, amount)
		{
		}

		public YellowScales(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
		}
	}
}