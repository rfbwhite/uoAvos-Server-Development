﻿using Server.Mobiles;

namespace Server.Commands.Generic
{
	public class KillCommand : BaseCommand
	{
		private readonly bool m_Value;

		public KillCommand(bool value)
		{
			m_Value = value;

			AccessLevel = AccessLevel.GameMaster;
			Supports = CommandSupport.AllMobiles;
			Commands = value ? new string[] { "Kill" } : new string[] { "Resurrect", "Res" };
			ObjectTypes = ObjectTypes.Mobiles;

			if (value)
			{
				Usage = "Kill";
				Description = "Kills a targeted player or npc.";
			}
			else
			{
				Usage = "Resurrect";
				Description = "Resurrects a targeted ghost.";
			}
		}

		public override void Execute(CommandEventArgs e, object obj)
		{
			var mob = (Mobile)obj;
			var from = e.Mobile;

			if (m_Value)
			{
				if (!mob.Alive)
				{
					LogFailure("They are already dead.");
				}
				else if (!mob.CanBeDamaged())
				{
					LogFailure("They cannot be harmed.");
				}
				else
				{
					CommandLogging.WriteLine(from, "{0} {1} killing {2}", from.AccessLevel, CommandLogging.Format(from), CommandLogging.Format(mob));
					mob.Kill();

					AddResponse("They have been killed.");
				}
			}
			else
			{
				if (mob.IsDeadBondedPet)
				{
					var bc = mob as BaseCreature;

					if (bc != null)
					{
						CommandLogging.WriteLine(from, "{0} {1} resurrecting {2}", from.AccessLevel, CommandLogging.Format(from), CommandLogging.Format(mob));

						bc.PlaySound(0x214);
						bc.FixedEffect(0x376A, 10, 16);

						bc.ResurrectPet();

						AddResponse("It has been resurrected.");
					}
				}
				else if (!mob.Alive)
				{
					CommandLogging.WriteLine(from, "{0} {1} resurrecting {2}", from.AccessLevel, CommandLogging.Format(from), CommandLogging.Format(mob));

					mob.PlaySound(0x214);
					mob.FixedEffect(0x376A, 10, 16);

					mob.Resurrect();

					AddResponse("They have been resurrected.");
				}
				else
				{
					LogFailure("They are not dead.");
				}
			}
		}
	}
}