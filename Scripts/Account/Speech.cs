﻿
using System;

namespace Server.Misc
{
	public enum ProfanityAction
	{
		None,           // no action taken
		Disallow,       // speech is not displayed
		Criminal,       // makes the player criminal, not killable by guards
		CriminalAction, // makes the player criminal, can be killed by guards
		Disconnect,     // player is kicked
		Other           // some other implementation
	}

	public class ProfanityProtection
	{
		private static readonly bool Enabled = false;
		private static readonly ProfanityAction Action = ProfanityAction.Disallow; // change here what to do when profanity is detected

		public static void Initialize()
		{
			if (Enabled)
			{
				EventSink.Speech += new SpeechEventHandler(EventSink_Speech);
			}
		}

		private static bool OnProfanityDetected(Mobile from, string speech)
		{
			switch (Action)
			{
				case ProfanityAction.None: return true;
				case ProfanityAction.Disallow: return false;
				case ProfanityAction.Criminal: from.Criminal = true; return true;
				case ProfanityAction.CriminalAction: from.CriminalAction(false); return true;
				case ProfanityAction.Disconnect:
					{
						var ns = from.NetState;

						if (ns != null)
						{
							ns.Dispose();
						}

						return false;
					}
				default:
				case ProfanityAction.Other: // TODO: Provide custom implementation if this is chosen
					{
						return true;
					}
			}
		}

		private static void EventSink_Speech(SpeechEventArgs e)
		{
			var from = e.Mobile;

			if (from.AccessLevel > AccessLevel.Player)
			{
				return;
			}

			if (!NameVerification.Validate(e.Speech, 0, Int32.MaxValue, true, true, false, Int32.MaxValue, m_Exceptions, m_Disallowed, m_StartDisallowed))
			{
				e.Blocked = !OnProfanityDetected(from, e.Speech);
			}
		}

		public static char[] Exceptions => m_Exceptions;
		public static string[] StartDisallowed => m_StartDisallowed;
		public static string[] Disallowed => m_Disallowed;

		private static readonly char[] m_Exceptions = new char[]
			{
				' ', '-', '.', '\'', '"', ',', '_', '+', '=', '~', '`', '!', '^', '*', '\\', '/', ';', ':', '<', '>', '[', ']', '{', '}', '?', '|', '(', ')', '%', '$', '&', '#', '@'
			};

		private static readonly string[] m_StartDisallowed = new string[] { };

		private static readonly string[] m_Disallowed = new string[]
			{
				"jigaboo",
				"chigaboo",
				"wop",
				"kyke",
				"kike",
				"tit",
				"spic",
				"prick",
				"piss",
				"lezbo",
				"lesbo",
				"felatio",
				"dyke",
				"dildo",
				"chinc",
				"chink",
				"cunnilingus",
				"cum",
				"cocksucker",
				"cock",
				"clitoris",
				"clit",
				"ass",
				"hitler",
				"penis",
				"nigga",
				"nigger",
				"klit",
				"kunt",
				"jiz",
				"jism",
				"jerkoff",
				"jackoff",
				"goddamn",
				"fag",
				"blowjob",
				"bitch",
				"asshole",
				"dick",
				"pussy",
				"snatch",
				"cunt",
				"twat",
				"shit",
				"fuck"
			};
	}
}