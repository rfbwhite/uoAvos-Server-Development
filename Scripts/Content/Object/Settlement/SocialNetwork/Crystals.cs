﻿using Server.Network;
using Server.Targeting;

using System;
using System.Collections.Generic;

namespace Server.Items
{
	public class CrystalRecharge
	{
		public static readonly CrystalRecharge[] Table = new CrystalRecharge[]
			{
				new CrystalRecharge( typeof( Citrine ), 500 ),
				new CrystalRecharge( typeof( Amber ), 500 ),
				new CrystalRecharge( typeof( Tourmaline ), 750 ),
				new CrystalRecharge( typeof( Emerald ), 1000 ),
				new CrystalRecharge( typeof( Sapphire ), 1000 ),
				new CrystalRecharge( typeof( Amethyst ), 1000 ),
				new CrystalRecharge( typeof( StarSapphire ), 1250 ),
				new CrystalRecharge( typeof( Diamond ), 2000 )
			};

		public static CrystalRecharge Get(Type type)
		{
			foreach (var info in Table)
			{
				if (info.Type == type)
				{
					return info;
				}
			}

			return null;
		}

		private readonly Type m_Type;
		private readonly int m_Amount;

		public Type Type => m_Type;
		public int Amount => m_Amount;

		private CrystalRecharge(Type type, int amount)
		{
			m_Type = type;
			m_Amount = amount;
		}
	}

	public class BroadcastCrystal : Item
	{
		public static readonly int MaxCharges = 2000;

		public override int LabelNumber => 1060740;  // communication crystal

		private int m_Charges;
		private List<ReceiverCrystal> m_Receivers;

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Active
		{
			get => ItemID == 0x1ECD;
			set
			{
				ItemID = value ? 0x1ECD : 0x1ED0;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Charges
		{
			get => m_Charges;
			set
			{
				m_Charges = value;
				InvalidateProperties();
			}
		}

		public List<ReceiverCrystal> Receivers => m_Receivers;

		[Constructable]
		public BroadcastCrystal() : this(2000)
		{
		}

		[Constructable]
		public BroadcastCrystal(int charges) : base(0x1ED0)
		{
			Light = LightType.Circle150;

			m_Charges = charges;

			m_Receivers = new List<ReceiverCrystal>();
		}

		public BroadcastCrystal(Serial serial) : base(serial)
		{
		}

		public override void GetProperties(ObjectPropertyList list)
		{
			base.GetProperties(list);

			list.Add(Active ? 1060742 : 1060743); // active / inactive
			list.Add(1060745); // broadcast
			list.Add(1060741, Charges.ToString()); // charges: ~1_val~

			if (Receivers.Count > 0)
			{
				list.Add(1060746, Receivers.Count.ToString()); // links: ~1_val~
			}
		}

		public override void OnSingleClick(Mobile from)
		{
			base.OnSingleClick(from);

			LabelTo(from, Active ? 1060742 : 1060743); // active / inactive
			LabelTo(from, 1060745); // broadcast
			LabelTo(from, 1060741, Charges.ToString()); // charges: ~1_val~

			if (Receivers.Count > 0)
			{
				LabelTo(from, 1060746, Receivers.Count.ToString()); // links: ~1_val~
			}
		}

		public override bool HandlesOnSpeech => true;

		public override void OnSpeech(SpeechEventArgs e)
		{
			if (!Active || Receivers.Count == 0 || (RootParent != null && !(RootParent is Mobile)))
			{
				return;
			}

			if (e.Type == MessageType.Emote)
			{
				return;
			}

			var from = e.Mobile;
			var speech = e.Speech;

			foreach (var receiver in new List<ReceiverCrystal>(Receivers))
			{
				if (receiver.Deleted)
				{
					Receivers.Remove(receiver);
				}
				else if (Charges > 0)
				{
					receiver.TransmitMessage(from, speech);
					Charges--;
				}
				else
				{
					Active = false;
					break;
				}
			}
		}

		public override void OnDoubleClick(Mobile from)
		{
			if (!from.InRange(GetWorldLocation(), 2))
			{
				from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
				return;
			}

			from.Target = new InternalTarget(this);
		}

		private class InternalTarget : Target
		{
			private readonly BroadcastCrystal m_Crystal;

			public InternalTarget(BroadcastCrystal crystal) : base(2, false, TargetFlags.None)
			{
				m_Crystal = crystal;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (!m_Crystal.IsAccessibleTo(from))
				{
					return;
				}

				if (from.Map != m_Crystal.Map || !from.InRange(m_Crystal.GetWorldLocation(), 2))
				{
					from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
					return;
				}

				if (targeted == m_Crystal)
				{
					if (m_Crystal.Active)
					{
						m_Crystal.Active = false;
						from.SendLocalizedMessage(500672); // You turn the crystal off.
					}
					else
					{
						if (m_Crystal.Charges > 0)
						{
							m_Crystal.Active = true;
							from.SendLocalizedMessage(500673); // You turn the crystal on.
						}
						else
						{
							from.SendLocalizedMessage(500676); // This crystal is out of charges.
						}
					}
				}
				else if (targeted is ReceiverCrystal)
				{
					var receiver = (ReceiverCrystal)targeted;

					if (m_Crystal.Receivers.Count >= 10)
					{
						from.SendLocalizedMessage(1010042); // This broadcast crystal is already linked to 10 receivers.
					}
					else if (receiver.Sender == m_Crystal)
					{
						from.SendLocalizedMessage(500674); // This crystal is already linked with that crystal.
					}
					else if (receiver.Sender != null)
					{
						from.SendLocalizedMessage(1010043); // That receiver crystal is already linked to another broadcast crystal.
					}
					else
					{
						receiver.Sender = m_Crystal;
						from.SendLocalizedMessage(500675); // That crystal has been linked to this crystal.
					}
				}
				else if (targeted == from)
				{
					foreach (var receiver in new List<ReceiverCrystal>(m_Crystal.Receivers))
					{
						receiver.Sender = null;
					}

					from.SendLocalizedMessage(1010046); // You unlink the broadcast crystal from all of its receivers.
				}
				else
				{
					var targItem = targeted as Item;

					if (targItem != null && targItem.VerifyMove(from))
					{
						var info = CrystalRecharge.Get(targItem.GetType());

						if (info != null)
						{
							if (m_Crystal.Charges >= MaxCharges)
							{
								from.SendLocalizedMessage(500678); // This crystal is already fully charged.
							}
							else
							{
								targItem.Consume();

								if (m_Crystal.Charges + info.Amount >= MaxCharges)
								{
									m_Crystal.Charges = MaxCharges;
									from.SendLocalizedMessage(500679); // You completely recharge the crystal.
								}
								else
								{
									m_Crystal.Charges += info.Amount;
									from.SendLocalizedMessage(500680); // You recharge the crystal.
								}
							}

							return;
						}
					}

					from.SendLocalizedMessage(500681); // You cannot use this crystal on that.
				}
			}
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.WriteEncodedInt(0); // version

			writer.WriteEncodedInt(m_Charges);
			writer.WriteItemList<ReceiverCrystal>(m_Receivers);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadEncodedInt();

			m_Charges = reader.ReadEncodedInt();
			m_Receivers = reader.ReadStrongItemList<ReceiverCrystal>();
		}
	}

	public class ReceiverCrystal : Item
	{
		public override int LabelNumber => 1060740;  // communication crystal

		private BroadcastCrystal m_Sender;

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Active
		{
			get => ItemID == 0x1ED1;
			set
			{
				ItemID = value ? 0x1ED1 : 0x1ED0;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public BroadcastCrystal Sender
		{
			get => m_Sender;
			set
			{
				if (m_Sender != null)
				{
					m_Sender.Receivers.Remove(this);
					m_Sender.InvalidateProperties();
				}

				m_Sender = value;

				if (value != null)
				{
					value.Receivers.Add(this);
					value.InvalidateProperties();
				}
			}
		}

		[Constructable]
		public ReceiverCrystal() : base(0x1ED0)
		{
			Light = LightType.Circle150;
		}

		public ReceiverCrystal(Serial serial) : base(serial)
		{
		}

		public override void GetProperties(ObjectPropertyList list)
		{
			base.GetProperties(list);

			list.Add(Active ? 1060742 : 1060743); // active / inactive
			list.Add(1060744); // receiver
		}

		public override void OnSingleClick(Mobile from)
		{
			base.OnSingleClick(from);

			LabelTo(from, Active ? 1060742 : 1060743); // active / inactive
			LabelTo(from, 1060744); // receiver
		}

		public void TransmitMessage(Mobile from, string message)
		{
			if (!Active)
			{
				return;
			}

			var text = String.Format("{0} says {1}", from.Name, message);

			if (RootParent is Mobile)
			{
				((Mobile)RootParent).SendMessage(0x2B2, "Crystal: " + text);
			}
			else if (RootParent is Item)
			{
				((Item)RootParent).PublicOverheadMessage(MessageType.Regular, 0x2B2, false, "Crystal: " + text);
			}
			else
			{
				PublicOverheadMessage(MessageType.Regular, 0x2B2, false, text);
			}
		}

		public override void OnDoubleClick(Mobile from)
		{
			if (!from.InRange(GetWorldLocation(), 2))
			{
				from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
				return;
			}

			from.Target = new InternalTarget(this);
		}

		private class InternalTarget : Target
		{
			private readonly ReceiverCrystal m_Crystal;

			public InternalTarget(ReceiverCrystal crystal) : base(-1, false, TargetFlags.None)
			{
				m_Crystal = crystal;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (!m_Crystal.IsAccessibleTo(from))
				{
					return;
				}

				if (from.Map != m_Crystal.Map || !from.InRange(m_Crystal.GetWorldLocation(), 2))
				{
					from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
					return;
				}

				if (targeted == m_Crystal)
				{
					if (m_Crystal.Active)
					{
						m_Crystal.Active = false;
						from.SendLocalizedMessage(500672); // You turn the crystal off.
					}
					else
					{
						m_Crystal.Active = true;
						from.SendLocalizedMessage(500673); // You turn the crystal on.
					}
				}
				else if (targeted == from)
				{
					if (m_Crystal.Sender != null)
					{
						m_Crystal.Sender = null;
						from.SendLocalizedMessage(1010044); // You unlink the receiver crystal.
					}
					else
					{
						from.SendLocalizedMessage(1010045); // That receiver crystal is not linked.
					}
				}
				else
				{
					var targItem = targeted as Item;

					if (targItem != null && targItem.VerifyMove(from))
					{
						var info = CrystalRecharge.Get(targItem.GetType());

						if (info != null)
						{
							from.SendLocalizedMessage(500677); // This crystal cannot be recharged.
							return;
						}
					}

					from.SendLocalizedMessage(1010045); // That receiver crystal is not linked.
				}
			}
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.WriteEncodedInt(0); // version

			writer.Write(m_Sender);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadEncodedInt();

			m_Sender = reader.ReadItem<BroadcastCrystal>();
		}
	}
}