﻿using Server.Network;

namespace Server.Items
{
	[FlipableAttribute(0x1766, 0x1768)]
	public class Cloth : Item, IScissorable, IDyable, ICommodity
	{
		int ICommodity.DescriptionNumber => LabelNumber;
		bool ICommodity.IsDeedable => true;

		public override double DefaultWeight => 0.1;

		[Constructable]
		public Cloth() : this(1)
		{
		}

		[Constructable]
		public Cloth(int amount) : base(0x1766)
		{
			Stackable = true;
			Amount = amount;
		}

		public Cloth(Serial serial) : base(serial)
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

		public override void OnSingleClick(Mobile from)
		{
			var number = (Amount == 1) ? 1049124 : 1049123;

			from.Send(new MessageLocalized(Serial, ItemID, MessageType.Regular, 0x3B2, 3, number, "", Amount.ToString()));
		}

		public bool Scissor(Mobile from, Scissors scissors)
		{
			if (Deleted || !from.CanSee(this))
			{
				return false;
			}

			base.ScissorHelper(from, new Bandage(), 1);

			return true;
		}
	}
}