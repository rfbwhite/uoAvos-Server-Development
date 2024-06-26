﻿namespace Server.Items
{
	public class FarmableLettuce : FarmableCrop
	{
		public static int GetCropID()
		{
			return 3254;
		}

		public override Item GetCropObject()
		{
			var lettuce = new Lettuce {
				ItemID = Utility.Random(3184, 2)
			};

			return lettuce;
		}

		public override int GetPickedID()
		{
			return 3254;
		}

		[Constructable]
		public FarmableLettuce() : base(GetCropID())
		{
		}

		public FarmableLettuce(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.WriteEncodedInt(0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadEncodedInt();
		}
	}
}