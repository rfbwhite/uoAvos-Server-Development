﻿using Server.Network;

namespace Server.Items
{
	[FlipableAttribute(0xF95, 0xF96, 0xF97, 0xF98, 0xF99, 0xF9A, 0xF9B, 0xF9C)]
	public class BoltOfCloth : Item, IScissorable, IDyable, ICommodity
	{
		int ICommodity.DescriptionNumber => LabelNumber;
		bool ICommodity.IsDeedable => true;

		[Constructable]
		public BoltOfCloth() : this(1)
		{
		}

		[Constructable]
		public BoltOfCloth(int amount) : base(0xF95)
		{
			Stackable = true;
			Weight = 5.0;
			Amount = amount;
		}

		public BoltOfCloth(Serial serial) : base(serial)
		{
		}

		public bool Dye(Mobile from, DyeTub sender)
		{
			if (Deleted)
			{
				return false;
			}

			Hue = sender.DyedHue;

			return true;
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

		public bool Scissor(Mobile from, Scissors scissors)
		{
			if (Deleted || !from.CanSee(this))
			{
				return false;
			}

			base.ScissorHelper(from, new Cloth(), 50);

			return true;
		}

		public override void OnSingleClick(Mobile from)
		{
			var number = (Amount == 1) ? 1049122 : 1049121;

			from.Send(new MessageLocalized(Serial, ItemID, MessageType.Label, 0x3B2, 3, number, "", (Amount * 50).ToString()));
		}
	}
}