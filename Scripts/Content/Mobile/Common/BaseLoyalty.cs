﻿using Server.Items;
using Server.Network;

namespace Server.Mobiles
{
	#region Solen Hive Loyalty

	public class SolenHelper
	{
		/// Red Solen Ant Player Hive Loyalty
		public static bool CheckRedFriendship(Mobile m)
		{
			if (m is BaseCreature)
			{
				var bc = (BaseCreature)m;

				if (bc.Controlled && bc.ControlMaster is PlayerMobile)
				{
					return CheckRedFriendship(bc.ControlMaster);
				}
				else if (bc.Summoned && bc.SummonMaster is PlayerMobile)
				{
					return CheckRedFriendship(bc.SummonMaster);
				}
			}

			var player = m as PlayerMobile;

			return player != null && player.SolenFriendship == SolenFriendship.Red;
		}

		public static void OnRedDamage(Mobile from)
		{
			if (from is BaseCreature)
			{
				var bc = (BaseCreature)from;

				if (bc.Controlled && bc.ControlMaster is PlayerMobile)
				{
					OnRedDamage(bc.ControlMaster);
				}
				else if (bc.Summoned && bc.SummonMaster is PlayerMobile)
				{
					OnRedDamage(bc.SummonMaster);
				}
			}

			var player = from as PlayerMobile;

			if (player != null && player.SolenFriendship == SolenFriendship.Red)
			{
				player.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1054103); // The solen revoke their friendship. You will now be considered an intruder.

				player.SolenFriendship = SolenFriendship.None;
			}
		}


		/// Black Solen Ant Player Hive Loyalty
		public static bool CheckBlackFriendship(Mobile m)
		{
			if (m is BaseCreature)
			{
				var bc = (BaseCreature)m;

				if (bc.Controlled && bc.ControlMaster is PlayerMobile)
				{
					return CheckBlackFriendship(bc.ControlMaster);
				}
				else if (bc.Summoned && bc.SummonMaster is PlayerMobile)
				{
					return CheckBlackFriendship(bc.SummonMaster);
				}
			}

			var player = m as PlayerMobile;

			return player != null && player.SolenFriendship == SolenFriendship.Black;
		}

		public static void OnBlackDamage(Mobile from)
		{
			if (from is BaseCreature)
			{
				var bc = (BaseCreature)from;

				if (bc.Controlled && bc.ControlMaster is PlayerMobile)
				{
					OnBlackDamage(bc.ControlMaster);
				}
				else if (bc.Summoned && bc.SummonMaster is PlayerMobile)
				{
					OnBlackDamage(bc.SummonMaster);
				}
			}

			var player = from as PlayerMobile;

			if (player != null && player.SolenFriendship == SolenFriendship.Black)
			{
				player.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1054103); // The solen revoke their friendship. You will now be considered an intruder.

				player.SolenFriendship = SolenFriendship.None;
			}
		}


		/// Pack Special Item(s) In Solen Corpse
		public static void PackPicnicBasket(BaseCreature solen)
		{
			if (1 > Utility.Random(100))
			{
				var basket = new PicnicBasket();

				basket.DropItem(new BeverageBottle(BeverageType.Wine));
				basket.DropItem(new CheeseWedge());

				solen.PackItem(basket);
			}
		}
	}

	#endregion
}