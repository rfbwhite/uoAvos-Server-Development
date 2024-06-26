﻿
using System;

namespace Server.Items
{
	public class AgilityPotion : BaseAgilityPotion
	{
		public override int DexOffset => 10;
		public override TimeSpan Duration => TimeSpan.FromMinutes(2.0);

		[Constructable]
		public AgilityPotion() : base(PotionEffect.Agility)
		{
		}

		public AgilityPotion(Serial serial) : base(serial)
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

	public class GreaterAgilityPotion : BaseAgilityPotion
	{
		public override int DexOffset => 20;
		public override TimeSpan Duration => TimeSpan.FromMinutes(2.0);

		[Constructable]
		public GreaterAgilityPotion() : base(PotionEffect.AgilityGreater)
		{
		}

		public GreaterAgilityPotion(Serial serial) : base(serial)
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