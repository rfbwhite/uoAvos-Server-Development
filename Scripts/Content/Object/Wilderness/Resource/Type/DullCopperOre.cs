﻿namespace Server.Items
{
	public class DullCopperOre : BaseOre
	{
		[Constructable]
		public DullCopperOre() : this(1)
		{
		}

		[Constructable]
		public DullCopperOre(int amount) : base(CraftResource.DullCopper, amount)
		{
		}

		public DullCopperOre(Serial serial) : base(serial)
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

		public override BaseIngot GetIngot()
		{
			return new DullCopperIngot();
		}
	}
}