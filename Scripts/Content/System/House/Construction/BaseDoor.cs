﻿using Server.Commands;
using Server.ContextMenus;
using Server.Multis;
using Server.Network;
using Server.Targeting;

using System;
using System.Collections.Generic;

namespace Server.Items
{
	public enum DoorFacing
	{
		WestCW,
		EastCCW,
		WestCCW,
		EastCW,
		SouthCW,
		NorthCCW,
		SouthCCW,
		NorthCW,
		//Sliding Doors
		SouthSW,
		SouthSE,
		WestSS,
		WestSN
	}

	public abstract class BaseDoor : Item, ILockable, ITelekinesisable
	{
		private bool m_Open, m_Locked;
		private int m_OpenedID, m_OpenedSound;
		private int m_ClosedID, m_ClosedSound;
		private Point3D m_Offset;
		private BaseDoor m_Link;
		private uint m_KeyValue;

		private Timer m_Timer;

		private static readonly Point3D[] m_Offsets = new Point3D[]
			{
				new Point3D(-1, 1, 0 ),
				new Point3D( 1, 1, 0 ),
				new Point3D(-1, 0, 0 ),
				new Point3D( 1,-1, 0 ),
				new Point3D( 1, 1, 0 ),
				new Point3D( 1,-1, 0 ),
				new Point3D( 0, 0, 0 ),
				new Point3D( 0,-1, 0 ),

				new Point3D( 0, 0, 0 ),
				new Point3D( 0, 0, 0 ),
				new Point3D( 0, 0, 0 ),
				new Point3D( 0, 0, 0 )
			};

		// Called by RunUO
		public static void Initialize()
		{
			EventSink.OpenDoorMacroUsed += new OpenDoorMacroEventHandler(EventSink_OpenDoorMacroUsed);

			CommandSystem.Register("Link", AccessLevel.GameMaster, new CommandEventHandler(Link_OnCommand));
			CommandSystem.Register("ChainLink", AccessLevel.GameMaster, new CommandEventHandler(ChainLink_OnCommand));
		}

		[Usage("Link")]
		[Description("Links two targeted doors together.")]
		private static void Link_OnCommand(CommandEventArgs e)
		{
			e.Mobile.BeginTarget(-1, false, TargetFlags.None, new TargetCallback(Link_OnFirstTarget));
			e.Mobile.SendMessage("Target the first door to link.");
		}

		private static void Link_OnFirstTarget(Mobile from, object targeted)
		{
			var door = targeted as BaseDoor;

			if (door == null)
			{
				from.BeginTarget(-1, false, TargetFlags.None, new TargetCallback(Link_OnFirstTarget));
				from.SendMessage("That is not a door. Try again.");
			}
			else
			{
				from.BeginTarget(-1, false, TargetFlags.None, new TargetStateCallback(Link_OnSecondTarget), door);
				from.SendMessage("Target the second door to link.");
			}
		}

		private static void Link_OnSecondTarget(Mobile from, object targeted, object state)
		{
			var first = (BaseDoor)state;
			var second = targeted as BaseDoor;

			if (second == null)
			{
				from.BeginTarget(-1, false, TargetFlags.None, new TargetStateCallback(Link_OnSecondTarget), first);
				from.SendMessage("That is not a door. Try again.");
			}
			else
			{
				first.Link = second;
				second.Link = first;
				from.SendMessage("The doors have been linked.");
			}
		}

		[Usage("ChainLink")]
		[Description("Chain-links two or more targeted doors together.")]
		private static void ChainLink_OnCommand(CommandEventArgs e)
		{
			e.Mobile.BeginTarget(-1, false, TargetFlags.None, new TargetStateCallback(ChainLink_OnTarget), new List<BaseDoor>());
			e.Mobile.SendMessage("Target the first of a sequence of doors to link.");
		}

		private static void ChainLink_OnTarget(Mobile from, object targeted, object state)
		{
			var door = targeted as BaseDoor;

			if (door == null)
			{
				from.BeginTarget(-1, false, TargetFlags.None, new TargetStateCallback(ChainLink_OnTarget), state);
				from.SendMessage("That is not a door. Try again.");
			}
			else
			{
				var list = (List<BaseDoor>)state;

				if (list.Count > 0 && list[0] == door)
				{
					if (list.Count >= 2)
					{
						for (var i = 0; i < list.Count; ++i)
						{
							list[i].Link = list[(i + 1) % list.Count];
						}

						from.SendMessage("The chain of doors have been linked.");
					}
					else
					{
						from.BeginTarget(-1, false, TargetFlags.None, new TargetStateCallback(ChainLink_OnTarget), state);
						from.SendMessage("You have not yet targeted two unique doors. Target the second door to link.");
					}
				}
				else if (list.Contains(door))
				{
					from.BeginTarget(-1, false, TargetFlags.None, new TargetStateCallback(ChainLink_OnTarget), state);
					from.SendMessage("You have already targeted that door. Target another door, or retarget the first door to complete the chain.");
				}
				else
				{
					list.Add(door);

					from.BeginTarget(-1, false, TargetFlags.None, new TargetStateCallback(ChainLink_OnTarget), state);

					if (list.Count == 1)
					{
						from.SendMessage("Target the second door to link.");
					}
					else
					{
						from.SendMessage("Target another door to link. To complete the chain, retarget the first door.");
					}
				}
			}
		}

		private static void EventSink_OpenDoorMacroUsed(OpenDoorMacroEventArgs args)
		{
			var m = args.Mobile;

			if (m.Map != null)
			{
				int x = m.X, y = m.Y;

				switch (m.Direction & Direction.Mask)
				{
					case Direction.North: --y; break;
					case Direction.Right: ++x; --y; break;
					case Direction.East: ++x; break;
					case Direction.Down: ++x; ++y; break;
					case Direction.South: ++y; break;
					case Direction.Left: --x; ++y; break;
					case Direction.West: --x; break;
					case Direction.Up: --x; --y; break;
				}

				var sector = m.Map.GetSector(x, y);

				foreach (var item in sector.Items)
				{
					if (item.Location.X == x && item.Location.Y == y && (item.Z + item.ItemData.Height) > m.Z && (m.Z + 16) > item.Z && item is BaseDoor && m.CanSee(item) && m.InLOS(item))
					{
						if (m.CheckAlive())
						{
							m.SendLocalizedMessage(500024); // Opening door...
							item.OnDoubleClick(m);
						}

						break;
					}
				}
			}
		}

		public static Point3D GetOffset(DoorFacing facing)
		{
			return m_Offsets[(int)facing];
		}

		private class InternalTimer : Timer
		{
			private readonly BaseDoor m_Door;

			public InternalTimer(BaseDoor door) : base(TimeSpan.FromSeconds(20.0), TimeSpan.FromSeconds(10.0))
			{
				Priority = TimerPriority.OneSecond;
				m_Door = door;
			}

			protected override void OnTick()
			{
				if (m_Door.Open && m_Door.IsFreeToClose())
				{
					m_Door.Open = false;
				}
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Locked
		{
			get => m_Locked;
			set => m_Locked = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public uint KeyValue
		{
			get => m_KeyValue;
			set => m_KeyValue = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Open
		{
			get => m_Open;
			set
			{
				if (m_Open != value)
				{
					m_Open = value;

					ItemID = m_Open ? m_OpenedID : m_ClosedID;

					if (m_Open)
					{
						Location = new Point3D(X + m_Offset.X, Y + m_Offset.Y, Z + m_Offset.Z);
					}
					else
					{
						Location = new Point3D(X - m_Offset.X, Y - m_Offset.Y, Z - m_Offset.Z);
					}

					Effects.PlaySound(this, Map, m_Open ? m_OpenedSound : m_ClosedSound);

					if (m_Open)
					{
						m_Timer.Start();
					}
					else
					{
						m_Timer.Stop();
					}
				}
			}
		}

		public bool CanClose()
		{
			if (!m_Open)
			{
				return true;
			}

			var map = Map;

			if (map == null)
			{
				return false;
			}

			var p = new Point3D(X - m_Offset.X, Y - m_Offset.Y, Z - m_Offset.Z);

			return CheckFit(map, p, 16);
		}

		private bool CheckFit(Map map, Point3D p, int height)
		{
			if (map == Map.Internal)
			{
				return false;
			}

			var x = p.X;
			var y = p.Y;
			var z = p.Z;

			var sector = map.GetSector(x, y);
			var items = sector.Items;
			var mobs = sector.Mobiles;

			for (var i = 0; i < items.Count; ++i)
			{
				var item = items[i];

				if (!(item is BaseMulti) && item.ItemID <= TileData.MaxItemValue && item.AtWorldPoint(x, y) && !(item is BaseDoor))
				{
					var id = item.ItemData;
					var surface = id.Surface;
					var impassable = id.Impassable;

					if ((surface || impassable) && (item.Z + id.CalcHeight) > z && (z + height) > item.Z)
					{
						return false;
					}
				}
			}

			for (var i = 0; i < mobs.Count; ++i)
			{
				var m = mobs[i];

				if (m.Location.X == x && m.Location.Y == y)
				{
					if (m.Hidden && m.AccessLevel > AccessLevel.Player)
					{
						continue;
					}

					if (!m.Alive)
					{
						continue;
					}

					if ((m.Z + 16) > z && (z + height) > m.Z)
					{
						return false;
					}
				}
			}

			return true;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int OpenedID
		{
			get => m_OpenedID;
			set => m_OpenedID = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ClosedID
		{
			get => m_ClosedID;
			set => m_ClosedID = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int OpenedSound
		{
			get => m_OpenedSound;
			set => m_OpenedSound = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ClosedSound
		{
			get => m_ClosedSound;
			set => m_ClosedSound = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Point3D Offset
		{
			get => m_Offset;
			set => m_Offset = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public BaseDoor Link
		{
			get
			{
				if (m_Link != null && m_Link.Deleted)
				{
					m_Link = null;
				}

				return m_Link;
			}
			set => m_Link = value;
		}

		public virtual bool UseChainedFunctionality => false;

		public List<BaseDoor> GetChain()
		{
			var list = new List<BaseDoor>();
			var c = this;

			do
			{
				list.Add(c);
				c = c.Link;
			} while (c != null && !list.Contains(c));

			return list;
		}

		public bool IsFreeToClose()
		{
			if (!UseChainedFunctionality)
			{
				return CanClose();
			}

			var list = GetChain();

			var freeToClose = true;

			for (var i = 0; freeToClose && i < list.Count; ++i)
			{
				freeToClose = list[i].CanClose();
			}

			return freeToClose;
		}

		public void OnTelekinesis(Mobile from)
		{
			Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5022);
			Effects.PlaySound(Location, Map, 0x1F5);

			Use(from);
		}

		public virtual bool IsInside(Mobile from)
		{
			return false;
		}

		public virtual bool UseLocks()
		{
			return true;
		}

		public virtual void Use(Mobile from)
		{
			if (m_Locked && !m_Open && UseLocks())
			{
				if (from.AccessLevel >= AccessLevel.GameMaster)
				{
					from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 502502); // That is locked, but you open it with your godly powers.
																				   //from.Send( new MessageLocalized( Serial, ItemID, MessageType.Regular, 0x3B2, 3, 502502, "", "" ) ); // That is locked, but you open it with your godly powers.
				}
				else if (Key.ContainsKey(from.Backpack, KeyValue))
				{
					from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 501282); // You quickly unlock, open, and relock the door
				}
				else if (IsInside(from))
				{
					from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 501280); // That is locked, but is usable from the inside.
				}
				else
				{
					if (Hue == 0x44E && Map == Map.Malas) // doom door into healer room in doom
					{
						SendLocalizedMessageTo(from, 1060014); // Only the dead may pass.
					}
					else
					{
						from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 502503); // That is locked.
					}

					return;
				}
			}

			if (m_Open && !IsFreeToClose())
			{
				return;
			}

			if (m_Open)
			{
				OnClosed(from);
			}
			else
			{
				OnOpened(from);
			}

			if (UseChainedFunctionality)
			{
				var open = !m_Open;

				var list = GetChain();

				for (var i = 0; i < list.Count; ++i)
				{
					list[i].Open = open;
				}
			}
			else
			{
				Open = !m_Open;

				var link = Link;

				if (m_Open && link != null && !link.Open)
				{
					link.Open = true;
				}
			}
		}

		public virtual void OnOpened(Mobile from)
		{
		}

		public virtual void OnClosed(Mobile from)
		{
		}

		public override void OnDoubleClick(Mobile from)
		{
			if (from.AccessLevel == AccessLevel.Player && (/*!from.InLOS( this ) || */!from.InRange(GetWorldLocation(), 2)))
			{
				from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
			}
			else
			{
				Use(from);
			}
		}

		public BaseDoor(int closedID, int openedID, int openedSound, int closedSound, Point3D offset) : base(closedID)
		{
			m_OpenedID = openedID;
			m_ClosedID = closedID;
			m_OpenedSound = openedSound;
			m_ClosedSound = closedSound;
			m_Offset = offset;

			m_Timer = new InternalTimer(this);

			Movable = false;
		}

		public BaseDoor(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0); // version

			writer.Write(m_KeyValue);

			writer.Write(m_Open);
			writer.Write(m_Locked);
			writer.Write(m_OpenedID);
			writer.Write(m_ClosedID);
			writer.Write(m_OpenedSound);
			writer.Write(m_ClosedSound);
			writer.Write(m_Offset);
			writer.Write(m_Link);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();

			switch (version)
			{
				case 0:
					{
						m_KeyValue = reader.ReadUInt();
						m_Open = reader.ReadBool();
						m_Locked = reader.ReadBool();
						m_OpenedID = reader.ReadInt();
						m_ClosedID = reader.ReadInt();
						m_OpenedSound = reader.ReadInt();
						m_ClosedSound = reader.ReadInt();
						m_Offset = reader.ReadPoint3D();
						m_Link = reader.ReadItem() as BaseDoor;

						m_Timer = new InternalTimer(this);

						if (m_Open)
						{
							m_Timer.Start();
						}

						break;
					}
			}
		}
	}

	public abstract class BaseHouseDoor : BaseDoor, ISecurable
	{
		private DoorFacing m_Facing;
		private SecureLevel m_Level;

		[CommandProperty(AccessLevel.GameMaster)]
		public DoorFacing Facing
		{
			get => m_Facing;
			set => m_Facing = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public SecureLevel Level
		{
			get => m_Level;
			set => m_Level = value;
		}

		public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
		{
			base.GetContextMenuEntries(from, list);
			SetSecureLevelEntry.AddTo(from, this, list);
		}

		public BaseHouseDoor(DoorFacing facing, int closedID, int openedID, int openedSound, int closedSound, Point3D offset) : base(closedID, openedID, openedSound, closedSound, offset)
		{
			m_Facing = facing;
			m_Level = SecureLevel.Anyone;
		}

		public BaseHouse FindHouse()
		{
			Point3D loc;

			if (Open)
			{
				loc = new Point3D(X - Offset.X, Y - Offset.Y, Z - Offset.Z);
			}
			else
			{
				loc = Location;
			}

			return BaseHouse.FindHouseAt(loc, Map, 20);
		}

		public bool CheckAccess(Mobile m)
		{
			var house = FindHouse();

			if (house == null)
			{
				return false;
			}

			if (!house.IsAosRules)
			{
				return true;
			}

			if (house.Public ? house.IsBanned(m) : !house.HasAccess(m))
			{
				return false;
			}

			return house.HasSecureAccess(m, m_Level);
		}

		public override void OnOpened(Mobile from)
		{
			var house = FindHouse();

			if (house != null && house.IsFriend(from) && from.AccessLevel == AccessLevel.Player && house.RefreshDecay())
			{
				from.SendLocalizedMessage(1043293); // Your house's age and contents have been refreshed.
			}

			if (house != null && house.Public && !house.IsFriend(from))
			{
				house.Visits++;
			}
		}

		public override bool UseLocks()
		{
			var house = FindHouse();

			return (house == null || !house.IsAosRules);
		}

		public override void Use(Mobile from)
		{
			if (!CheckAccess(from))
			{
				from.SendLocalizedMessage(1061637); // You are not allowed to access this.
			}
			else
			{
				base.Use(from);
			}
		}

		public BaseHouseDoor(Serial serial) : base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(1); // version

			writer.Write((int)m_Level);

			writer.Write((int)m_Facing);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();

			switch (version)
			{
				case 1:
					{
						m_Level = (SecureLevel)reader.ReadInt();
						goto case 0;
					}
				case 0:
					{
						if (version < 1)
						{
							m_Level = SecureLevel.Anyone;
						}

						m_Facing = (DoorFacing)reader.ReadInt();
						break;
					}
			}
		}

		public override bool IsInside(Mobile from)
		{
			int x, y, w, h;

			const int r = 2;
			const int bs = r * 2 + 1;
			const int ss = r + 1;

			switch (m_Facing)
			{
				case DoorFacing.WestCW:
				case DoorFacing.EastCCW: x = -r; y = -r; w = bs; h = ss; break;

				case DoorFacing.EastCW:
				case DoorFacing.WestCCW: x = -r; y = 0; w = bs; h = ss; break;

				case DoorFacing.SouthCW:
				case DoorFacing.NorthCCW: x = -r; y = -r; w = ss; h = bs; break;

				case DoorFacing.NorthCW:
				case DoorFacing.SouthCCW: x = 0; y = -r; w = ss; h = bs; break;

				//No way to test the 'insideness' of SE Sliding doors on OSI, so leaving them default to false until furthur information gained

				default: return false;
			}

			var rx = from.X - X;
			var ry = from.Y - Y;
			var az = Math.Abs(from.Z - Z);

			return (rx >= x && rx < (x + w) && ry >= y && ry < (y + h) && az <= 4);
		}
	}
}