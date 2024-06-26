using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Targeting;

using System;

namespace Server.Spells.Druid
{
	public class NaturesPassageSpell : DruidSpell
	{
		public override TimeSpan CastDelayBase => TimeSpan.FromSeconds(0.5);

		private readonly RunebookEntry m_Entry;
		private readonly Runebook m_Book;

		public NaturesPassageSpell(Mobile caster, Item scroll)
			: this(caster, scroll, null, null)
		{
		}

		public NaturesPassageSpell(Mobile caster, Item scroll, RunebookEntry entry, Runebook book)
			: base(caster, scroll, DruidSpellName.NaturesPassage)
		{
			m_Entry = entry;
			m_Book = book;
		}

		public override void OnCast()
		{
			if (m_Entry == null)
			{
				Caster.Target = new InternalTarget(this);
			}
			else
			{
				Effect(m_Entry.Location, m_Entry.Map, true);
			}
		}

		public override bool CheckCast()
		{
			if (Caster.Criminal)
			{
				Caster.SendLocalizedMessage(1005561, "", 0x22); // Thou'rt a criminal and cannot escape so easily.
				return false;
			}

			if (SpellHelper.CheckCombat(Caster))
			{
				Caster.SendLocalizedMessage(1005564, "", 0x22); // Wouldst thou flee during the heat of battle??
				return false;
			}

			if (WeightOverloading.IsOverloaded(Caster))
			{
				Caster.SendLocalizedMessage(502359, "", 0x22); // Thou art too encumbered to move.
				return false;
			}

			return SpellHelper.CheckTravel(Caster, TravelCheckType.RecallFrom);
		}

		public void Effect(Point3D loc, Map map, bool checkMulti)
		{
			if (map == null || (!Core.AOS && Caster.Map != map))
			{
				Caster.SendLocalizedMessage(1005569); // You can not recall to another facet.
			}
			else if (!SpellHelper.CheckTravel(Caster, TravelCheckType.RecallFrom))
			{
			}
			else if (!SpellHelper.CheckTravel(Caster, map, loc, TravelCheckType.RecallTo))
			{
			}
			else if (Caster.Murderer && map != Map.Felucca)
			{
				Caster.SendLocalizedMessage(1019004); // You are not allowed to travel there.
			}
			else if (Caster.Criminal)
			{
				Caster.SendLocalizedMessage(1005561, "", 0x22); // Thou'rt a criminal and cannot escape so easily.
			}
			else if (SpellHelper.CheckCombat(Caster))
			{
				Caster.SendLocalizedMessage(1005564, "", 0x22); // Wouldst thou flee during the heat of battle??
			}
			else if (WeightOverloading.IsOverloaded(Caster))
			{
				Caster.SendLocalizedMessage(502359, "", 0x22); // Thou art too encumbered to move.
			}
			else if (!map.CanSpawnMobile(loc.X, loc.Y, loc.Z))
			{
				Caster.SendLocalizedMessage(501942); // That location is blocked.
			}
			else if (checkMulti && SpellHelper.CheckMulti(loc, map))
			{
				Caster.SendLocalizedMessage(501942); // That location is blocked.
			}
			else if (m_Book != null && m_Book.CurCharges <= 0)
			{
				Caster.SendLocalizedMessage(502412); // There are no charges left on that item.
			}
			else if (CheckSequence())
			{
				BaseCreature.TeleportPets(Caster, loc, map, true);

				if (m_Book != null)
				{
					--m_Book.CurCharges;
				}

				Caster.PlaySound(0x19);

				Effects.SendLocationParticles(Caster, 0xC87, 9, 10, 5025);

				Caster.MoveToWorld(loc, map);

				Caster.PlaySound(0x19);

				Effects.SendLocationParticles(Caster, 0xC87, 9, 10, 5025);
			}

			FinishSequence();
		}

		private class InternalTarget : Target
		{
			private readonly NaturesPassageSpell m_Owner;

			public InternalTarget(NaturesPassageSpell owner) 
				: base(12, false, TargetFlags.None)
			{
				m_Owner = owner;

				owner.Caster.LocalOverheadMessage(MessageType.Regular, 0x3B2, 501029); // Select Marked item.
			}

			protected override void OnTarget(Mobile from, object o)
			{
				if (o is RecallRune rune)
				{
					if (rune.Marked)
					{
						m_Owner.Effect(rune.Target, rune.TargetMap, true);
					}
					else
					{
						from.SendLocalizedMessage(501805); // That rune is not yet marked.
					}
				}
				else if (o is Runebook rb)
				{
					var e = rb.Default;

					if (e != null)
					{
						m_Owner.Effect(e.Location, e.Map, true);
					}
					else
					{
						from.SendLocalizedMessage(502354); // Target is not marked.
					}
				}
				else if (o is Key k && k.KeyValue != 0 && k.Link is BaseBoat boat)
				{
					if (!boat.Deleted && boat.CheckKey(k.KeyValue))
					{
						m_Owner.Effect(boat.GetMarkedLocation(), boat.Map, false);
					}
					else
					{
						from.SendLocalizedMessage(502357); // I can not recall from that object.
					}
				}
				else
				{
					from.SendLocalizedMessage(502357); // I can not recall from that object.
				}
			}

			protected override void OnTargetFinish(Mobile from)
			{
				m_Owner.FinishSequence();
			}
		}
	}
}
