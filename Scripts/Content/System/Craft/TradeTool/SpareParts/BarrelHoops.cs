﻿namespace Server.Items
{
	public class BarrelHoops : Item
	{
		public override int LabelNumber => 1011228;  // Barrel hoops

		[Constructable]
		public BarrelHoops() : base(0x1DB7)
		{
			Weight = 5;
		}

		public BarrelHoops(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
		}
	}
}