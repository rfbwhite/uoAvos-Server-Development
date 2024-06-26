﻿namespace Server.Items
{
	public class UnbakedPumpkinPie : CookableFood
	{
		public override int LabelNumber => 1041342;  // unbaked pumpkin pie

		[Constructable]
		public UnbakedPumpkinPie() : base(0x1042, 25)
		{
			Weight = 1.0;
		}

		public UnbakedPumpkinPie(Serial serial) : base(serial)
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

		public override Food Cook()
		{
			return new PumpkinPie();
		}
	}
}