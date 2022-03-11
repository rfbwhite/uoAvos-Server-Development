﻿using Server.ContextMenus;
using Server.Engines.Quests.Definitions;
using Server.Items;
using Server.Mobiles;
using Server.Network;

using System.Collections.Generic;

namespace Server.Engines.Quests.Mobiles
{
	public class Horus : BaseQuester
	{
		[Constructable]
		public Horus() : base("the Guardian")
		{
		}

		public override void InitBody()
		{
			InitStats(100, 100, 25);

			Hue = 0x83F3;
			Body = 0x190;

			Name = "Horus";
		}

		public override void InitOutfit()
		{
			AddItem(SetHue(new PlateLegs(), 0x849));
			AddItem(SetHue(new PlateChest(), 0x849));
			AddItem(SetHue(new PlateArms(), 0x849));
			AddItem(SetHue(new PlateGloves(), 0x849));
			AddItem(SetHue(new PlateGorget(), 0x849));

			AddItem(SetHue(new Bardiche(), 0x482));

			AddItem(SetHue(new Boots(), 0x001));
			AddItem(SetHue(new Cloak(), 0x482));

			Utility.AssignRandomHair(this, false);
			Utility.AssignRandomFacialHair(this, false);
		}

		public override int GetAutoTalkRange(PlayerMobile m)
		{
			return 3;
		}

		public override bool CanTalkTo(PlayerMobile to)
		{
			var qs = to.Quest;

			return (qs is DarkTidesQuest && qs.IsObjectiveInProgress(typeof(FindCrystalCaveObjective_DarkTidesQuest)));
		}

		public override void OnTalk(PlayerMobile player, bool contextMenu)
		{
			var qs = player.Quest;

			if (qs is DarkTidesQuest)
			{
				var obj = qs.FindObjective(typeof(FindCrystalCaveObjective_DarkTidesQuest));

				if (obj != null && !obj.Completed)
				{
					obj.Complete();
				}
			}
		}

		public override void OnMovement(Mobile m, Point3D oldLocation)
		{
			base.OnMovement(m, oldLocation);

			if (InRange(m.Location, 2) && !InRange(oldLocation, 2) && m is PlayerMobile)
			{
				var pm = (PlayerMobile)m;
				var qs = pm.Quest;

				if (qs is DarkTidesQuest)
				{
					var obj = qs.FindObjective(typeof(ReturnToCrystalCaveObjective_DarkTidesQuest));

					if (obj != null && !obj.Completed)
					{
						obj.Complete();
					}
					else
					{
						obj = qs.FindObjective(typeof(FindHorusAboutRewardObjective_DarkTidesQuest));

						if (obj != null && !obj.Completed)
						{
							var cont = GetNewContainer();

							cont.DropItem(new Gold(500));

							BaseJewel jewel = new GoldBracelet();
							if (Core.AOS)
							{
								BaseRunicTool.ApplyAttributesTo(jewel, 3, 20, 40);
							}

							cont.DropItem(jewel);

							if (!pm.PlaceInBackpack(cont))
							{
								cont.Delete();
								pm.SendLocalizedMessage(1046260); // You need to clear some space in your inventory to continue with the quest.  Come back here when you have more space in your inventory.
							}
							else
							{
								obj.Complete();
							}
						}
					}
				}
			}
		}

		public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
		{
			base.GetContextMenuEntries(from, list);

			if (from.Alive)
			{
				var pm = from as PlayerMobile;

				if (pm != null)
				{
					var qs = pm.Quest;

					if (qs is DarkTidesQuest)
					{
						var obj = qs.FindObjective(typeof(SpeakCavePasswordObjective_DarkTidesQuest));
						var enabled = (obj != null && !obj.Completed);

						list.Add(new SpeakPasswordEntry(this, pm, enabled));
					}
				}
			}
		}

		public virtual void OnPasswordSpoken(PlayerMobile from)
		{
			var qs = from.Quest;

			if (qs is DarkTidesQuest)
			{
				var obj = qs.FindObjective(typeof(SpeakCavePasswordObjective_DarkTidesQuest));

				if (obj != null && !obj.Completed)
				{
					obj.Complete();
					return;
				}
			}

			from.SendLocalizedMessage(1060185); // Horus ignores you.
		}

		private class SpeakPasswordEntry : ContextMenuEntry
		{
			private readonly Horus m_Horus;
			private readonly PlayerMobile m_From;

			public SpeakPasswordEntry(Horus horus, PlayerMobile from, bool enabled) : base(6193, 3)
			{
				m_Horus = horus;
				m_From = from;

				if (!enabled)
				{
					Flags |= CMEFlags.Disabled;
				}
			}

			public override void OnClick()
			{
				if (m_From.Alive)
				{
					m_Horus.OnPasswordSpoken(m_From);
				}
			}
		}

		public Horus(Serial serial) : base(serial)
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