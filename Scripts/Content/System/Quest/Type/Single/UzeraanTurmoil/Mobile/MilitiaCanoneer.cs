﻿using Server.Engines.Quests.Items;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Quests.Mobiles
{
	public class MilitiaCanoneer : BaseQuester
	{
		private bool m_Active;

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Active
		{
			get => m_Active;
			set => m_Active = value;
		}

		[Constructable]
		public MilitiaCanoneer() : base("the Militia Canoneer")
		{
			m_Active = true;
		}

		public override void InitBody()
		{
			InitStats(100, 125, 25);

			Hue = Utility.RandomSkinHue();

			Female = false;
			Body = 0x190;
			Name = NameList.RandomName("male");
		}

		public override void InitOutfit()
		{
			Utility.AssignRandomHair(this);
			Utility.AssignRandomFacialHair(this, HairHue);

			AddItem(new PlateChest());
			AddItem(new PlateArms());
			AddItem(new PlateGloves());
			AddItem(new PlateLegs());

			var torch = new Torch {
				Movable = false
			};
			AddItem(torch);
			torch.Ignite();
		}

		public override bool CanTalkTo(PlayerMobile to)
		{
			return false;
		}

		public override void OnTalk(PlayerMobile player, bool contextMenu)
		{
		}

		public override bool IsEnemy(Mobile m)
		{
			if (m.Player || m is BaseVendor)
			{
				return false;
			}

			if (m is BaseCreature)
			{
				var bc = (BaseCreature)m;

				var master = bc.GetMaster();
				if (master != null)
				{
					return IsEnemy(master);
				}
			}

			return m.Karma < 0;
		}

		public bool WillFire(Cannon cannon, Mobile target)
		{
			if (m_Active && IsEnemy(target))
			{
				Direction = GetDirectionTo(target);
				Say(Utility.RandomList(500651, 1049098, 1049320, 1043149));
				return true;
			}

			return false;
		}

		public MilitiaCanoneer(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0); // version

			writer.Write(m_Active);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();

			m_Active = reader.ReadBool();
		}
	}
}