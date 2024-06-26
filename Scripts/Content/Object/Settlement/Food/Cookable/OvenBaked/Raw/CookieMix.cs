﻿namespace Server.Items
{
	public class CookieMix : CookableFood
	{
		[Constructable]
		public CookieMix() : base(0x103F, 20)
		{
			Weight = 1.0;
		}

		public override Food Cook()
		{
			return new Cookies();
		}

		public CookieMix(Serial serial) : base(serial)
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