﻿using System.Collections.Generic;

#region Developer Notations

/// In Select Shops There Should ALWAYS Be One Merchant That Sells Every Resources For Their Trade
/// In Select Shops There Should ALWAYS Be One Merchant That Sells Every TradeTools For Their Trade
/// In Select Shops There Should ALWAYS Be One Merchant That Sells Products Created From Their Trade

#endregion

namespace Server.Mobiles
{
	public class Carpenter : BaseVendor
	{
		private readonly List<SBInfo> m_SBInfos = new List<SBInfo>();
		protected override List<SBInfo> SBInfos => m_SBInfos;

		public override NpcGuild NpcGuild => NpcGuild.TinkersGuild;

		[Constructable]
		public Carpenter() : base("the carpenter")
		{
			SetSkill(SkillName.Carpentry, 85.0, 100.0);
			SetSkill(SkillName.Lumberjacking, 60.0, 83.0);
		}

		public override void InitSBInfo()
		{
			m_SBInfos.Add(new SBStavesWeapon());
			m_SBInfos.Add(new SBCarpenter());
			m_SBInfos.Add(new SBWoodenShields());

			if (IsTokunoVendor)
			{
				m_SBInfos.Add(new SBSECarpenter());
			}
		}

		public override void InitOutfit()
		{
			base.InitOutfit();

			AddItem(new Server.Items.HalfApron());
		}

		public Carpenter(Serial serial) : base(serial)
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