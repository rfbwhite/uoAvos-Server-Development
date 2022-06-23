﻿using Server.Engines.Quests.Definitions;
using Server.Items;
using Server.Mobiles;

using System;

namespace Server.Engines.Quests.Mobiles
{
	public class SummonedPaladin : BaseCreature
	{
		private PlayerMobile m_Necromancer;
		private bool m_ToDelete;

		public SummonedPaladin(PlayerMobile necromancer) : base(AIType.AI_Melee, FightMode.Aggressor, 10, 1, 0.2, 0.4)
		{
			m_Necromancer = necromancer;

			InitStats(45, 30, 5);
			Title = "the Paladin";

			Hue = 0x83F3;

			Female = false;
			Body = 0x190;
			Name = NameList.RandomName("male");

			Utility.AssignRandomHair(this);
			Utility.AssignRandomFacialHair(this, false);

			FacialHairHue = HairHue;

			AddItem(new Boots(0x1));
			AddItem(new ChainChest());
			AddItem(new ChainLegs());
			AddItem(new RingmailArms());
			AddItem(new PlateHelm());
			AddItem(new PlateGloves());
			AddItem(new PlateGorget());

			AddItem(new Cloak(0xCF));

			AddItem(new ThinLongsword());

			SetSkill(SkillName.Swords, 50.0);
			SetSkill(SkillName.Tactics, 50.0);

			PackGold(500);
		}

		public override bool IsHarmfulCriminal(Mobile target)
		{
			if (target == m_Necromancer)
			{
				return false;
			}

			return base.IsHarmfulCriminal(target);
		}

		public override bool ClickTitle => false;

		public override bool PlayerRangeSensitive => false;

		public override void OnThink()
		{
			if (!m_ToDelete && !Frozen)
			{
				if (m_Necromancer == null || m_Necromancer.Deleted || m_Necromancer.Map == Map.Internal)
				{
					Delete();
					return;
				}

				if (Combatant != m_Necromancer)
				{
					Combatant = m_Necromancer;
				}

				if (!m_Necromancer.Alive)
				{
					var qs = m_Necromancer.Quest;

					if (qs is DarkTidesQuest && qs.FindObjective(typeof(FindMardothEndObjective_DarkTidesQuest)) == null)
					{
						qs.AddObjective(new FindMardothEndObjective_DarkTidesQuest(false));
					}

					Say(1060139, m_Necromancer.Name); // You have made my work easy for me, ~1_NAME~.  My task here is done.

					m_ToDelete = true;

					Timer.DelayCall(TimeSpan.FromSeconds(5.0), Delete);
				}
				else if (m_Necromancer.Map != Map || GetDistanceToSqrt(m_Necromancer) > RangePerception + 1)
				{
					Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
					Effects.SendLocationParticles(EffectItem.Create(m_Necromancer.Location, m_Necromancer.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);

					Map = m_Necromancer.Map;
					Location = m_Necromancer.Location;

					PlaySound(0x1FE);

					Say(1060140); // You cannot escape me, knave of evil!
				}
			}

			base.OnThink();
		}

		public override void OnDeath(Container c)
		{
			base.OnDeath(c);

			var qs = m_Necromancer.Quest;

			if (qs is DarkTidesQuest && qs.FindObjective(typeof(FindMardothEndObjective_DarkTidesQuest)) == null)
			{
				qs.AddObjective(new FindMardothEndObjective_DarkTidesQuest(true));
			}
		}

		public SummonedPaladin(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0); // version

			writer.Write(m_Necromancer);
			writer.Write(m_ToDelete);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();

			m_Necromancer = reader.ReadMobile() as PlayerMobile;
			m_ToDelete = reader.ReadBool();

			if (m_ToDelete)
			{
				Delete();
			}
		}

		public static void BeginSummon(PlayerMobile player)
		{
			new SummonTimer(player).Start();
		}

		private class SummonTimer : Timer
		{
			private readonly PlayerMobile m_Player;
			private SummonedPaladin m_Paladin;
			private int m_Step;

			public SummonTimer(PlayerMobile player) : base(TimeSpan.FromSeconds(4.0))
			{
				Priority = TimerPriority.FiftyMS;

				m_Player = player;
			}

			protected override void OnTick()
			{
				if (m_Player.Deleted)
				{
					if (m_Step > 0)
					{
						m_Paladin.Delete();
					}

					return;
				}

				if (m_Step > 0 && m_Paladin.Deleted)
				{
					return;
				}

				if (m_Step == 0)
				{
					var moongate = new SummonedPaladinMoongate();
					moongate.MoveToWorld(new Point3D(2091, 1348, -90), Map.Malas);

					Effects.PlaySound(moongate.Location, moongate.Map, 0x20E);

					m_Paladin = new SummonedPaladin(m_Player) {
						Frozen = true,

						Location = moongate.Location,
						Map = moongate.Map
					};

					Delay = TimeSpan.FromSeconds(2.0);
					Start();
				}
				else if (m_Step == 1)
				{
					m_Paladin.Direction = m_Paladin.GetDirectionTo(m_Player);
					m_Paladin.Say(1060122); // STOP WICKED ONE!

					Delay = TimeSpan.FromSeconds(3.0);
					Start();
				}
				else
				{
					m_Paladin.Frozen = false;

					m_Paladin.Say(1060123); // I will slay you before I allow you to complete your evil rites!

					m_Paladin.Combatant = m_Player;
				}

				m_Step++;
			}
		}
	}

	public class SummonedPaladinMoongate : Item
	{
		public SummonedPaladinMoongate() : base(0xF6C)
		{
			Movable = false;
			Hue = 0x482;
			Light = LightType.Circle300;

			Timer.DelayCall(TimeSpan.FromSeconds(10.0), Delete);
		}

		public SummonedPaladinMoongate(Serial serial) : base(serial)
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

			Delete();
		}
	}
}