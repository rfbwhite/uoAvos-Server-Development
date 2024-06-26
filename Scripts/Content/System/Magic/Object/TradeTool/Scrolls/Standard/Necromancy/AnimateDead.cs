﻿namespace Server.Items
{
	public class AnimateDeadScroll : SpellScroll
	{
		[Constructable]
		public AnimateDeadScroll() : this(1)
		{
		}

		[Constructable]
		public AnimateDeadScroll(int amount) : base(SpellName.AnimateDead, 0x2260, amount)
		{
		}

		public AnimateDeadScroll(Serial serial) : base(serial)
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