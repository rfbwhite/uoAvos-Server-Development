﻿using Server.Items;

using System.Collections.Generic;

#region Developer Notations

/// In Select Shops There Should ALWAYS Be One Merchant That Sells Every Resources For Their Trade
/// In Select Shops There Should ALWAYS Be One Merchant That Sells Every TradeTools For Their Trade
/// In Select Shops There Should ALWAYS Be One Merchant That Sells Products Created From Their Trade

#endregion

namespace Server.Mobiles
{
	public class SBSECook : SBInfo
	{
		private readonly List<GenericBuyInfo> m_BuyInfo = new InternalBuyInfo();
		private readonly IShopSellInfo m_SellInfo = new InternalSellInfo();

		public SBSECook()
		{
		}

		public override IShopSellInfo SellInfo => m_SellInfo;
		public override List<GenericBuyInfo> BuyInfo => m_BuyInfo;

		public class InternalBuyInfo : List<GenericBuyInfo>
		{
			public InternalBuyInfo()
			{
				Add(new GenericBuyInfo(typeof(Wasabi), 2, 20, 0x24E8, 0));
				Add(new GenericBuyInfo(typeof(Wasabi), 2, 20, 0x24E9, 0));
				Add(new GenericBuyInfo(typeof(SushiRolls), 3, 20, 0x283E, 0));
				Add(new GenericBuyInfo(typeof(SushiPlatter), 3, 20, 0x2840, 0));
				Add(new GenericBuyInfo(typeof(GreenTeaCup), 3, 20, 0x284C, 0));
				Add(new GenericBuyInfo(typeof(MisoSoup), 3, 20, 0x284D, 0));
				Add(new GenericBuyInfo(typeof(WhiteMisoSoup), 3, 20, 0x284E, 0));
				Add(new GenericBuyInfo(typeof(RedMisoSoup), 3, 20, 0x284F, 0));
				Add(new GenericBuyInfo(typeof(AwaseMisoSoup), 3, 20, 0x2850, 0));
				Add(new GenericBuyInfo(typeof(BentoBox), 6, 20, 0x2836, 0));
				Add(new GenericBuyInfo(typeof(BentoBox), 6, 20, 0x2837, 0));
			}
		}

		public class InternalSellInfo : GenericSellInfo
		{
			public InternalSellInfo()
			{
				Add(typeof(Wasabi), 1);
				Add(typeof(BentoBox), 3);
				Add(typeof(GreenTeaCup), 1);
				Add(typeof(SushiRolls), 1);
				Add(typeof(SushiPlatter), 2);
				Add(typeof(MisoSoup), 1);
				Add(typeof(RedMisoSoup), 1);
				Add(typeof(WhiteMisoSoup), 1);
				Add(typeof(AwaseMisoSoup), 1);
			}
		}
	}
}