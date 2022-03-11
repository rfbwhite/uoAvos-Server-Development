﻿#if !MONO
using Microsoft.Win32.SafeHandles;
#pragma warning disable CA1416 // Validate platform compatibility
#endif

using Server.Guilds;
using Server.Network;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

#if !MONO
using System.Runtime.InteropServices;
#endif

using System.Threading;
using System.Threading.Tasks;

namespace Server
{
	public static class World
	{

		private static Dictionary<Serial, Mobile> m_Mobiles;
		private static Dictionary<Serial, Item> m_Items;

		private static bool m_Loading;
		private static bool m_Loaded;

		private static bool m_Saving;
		private static readonly ManualResetEvent m_DiskWriteHandle = new ManualResetEvent(true);

		private static Queue<IEntity> _addQueue, _deleteQueue;

		public static bool Saving => m_Saving;
		public static bool Loaded => m_Loaded;
		public static bool Loading => m_Loading;

		public static readonly string MobileIndexPath = Path.Combine("Export/Saves/Current/Mobiles/", "Mobiles.idx");
		public static readonly string MobileTypesPath = Path.Combine("Export/Saves/Current/Mobiles/", "Mobiles.tdb");
		public static readonly string MobileDataPath = Path.Combine("Export/Saves/Current/Mobiles/", "Mobiles.bin");

		public static readonly string ItemIndexPath = Path.Combine("Export/Saves/Current/Items/", "Items.idx");
		public static readonly string ItemTypesPath = Path.Combine("Export/Saves/Current/Items/", "Items.tdb");
		public static readonly string ItemDataPath = Path.Combine("Export/Saves/Current/Items/", "Items.bin");

		public static readonly string GuildIndexPath = Path.Combine("Export/Saves/Current/Guilds/", "Guilds.idx");
		public static readonly string GuildDataPath = Path.Combine("Export/Saves/Current/Guilds/", "Guilds.bin");

		public static void NotifyDiskWriteComplete()
		{
			if (m_DiskWriteHandle.Set())
			{
				Console.WriteLine("Closing Save Files. ");
			}
		}

		public static void WaitForWriteCompletion()
		{
			m_DiskWriteHandle.WaitOne();
		}

		public static Dictionary<Serial, Mobile> Mobiles => m_Mobiles;

		public static Dictionary<Serial, Item> Items => m_Items;

		public static bool OnDelete(IEntity entity)
		{
			if (m_Saving || m_Loading)
			{
				if (m_Saving)
				{
					AppendSafetyLog("delete", entity);
				}

				_deleteQueue.Enqueue(entity);

				return false;
			}

			return true;
		}

		public static void Broadcast(int hue, bool ascii, string text)
		{
			Packet p;

			if (ascii)
			{
				p = new AsciiMessage(Serial.MinusOne, -1, MessageType.Regular, hue, 3, "System", text);
			}
			else
			{
				p = new UnicodeMessage(Serial.MinusOne, -1, MessageType.Regular, hue, 3, "ENU", "System", text);
			}

			var list = NetState.Instances;

			p.Acquire();

			for (var i = 0; i < list.Count; ++i)
			{
				if (list[i].Mobile != null)
				{
					list[i].Send(p);
				}
			}

			p.Release();

			NetState.FlushAll();
		}

		public static void Broadcast(int hue, bool ascii, string format, params object[] args)
		{
			Broadcast(hue, ascii, String.Format(format, args));
		}

		private interface IEntityEntry
		{
			Serial Serial { get; }
			int TypeID { get; }
			long Position { get; }
			int Length { get; }
		}

		private sealed class GuildEntry : IEntityEntry
		{
			private readonly BaseGuild m_Guild;
			private readonly long m_Position;
			private readonly int m_Length;

			public BaseGuild Guild => m_Guild;

			public Serial Serial => m_Guild == null ? 0 : m_Guild.Id;

			public int TypeID => 0;

			public long Position => m_Position;

			public int Length => m_Length;

			public GuildEntry(BaseGuild g, long pos, int length)
			{
				m_Guild = g;
				m_Position = pos;
				m_Length = length;
			}
		}

		private sealed class ItemEntry : IEntityEntry
		{
			private readonly Item m_Item;
			private readonly int m_TypeID;
			private readonly string m_TypeName;
			private readonly long m_Position;
			private readonly int m_Length;

			public Item Item => m_Item;

			public Serial Serial => m_Item == null ? Serial.MinusOne : m_Item.Serial;

			public int TypeID => m_TypeID;

			public string TypeName => m_TypeName;

			public long Position => m_Position;

			public int Length => m_Length;

			public ItemEntry(Item item, int typeID, string typeName, long pos, int length)
			{
				m_Item = item;
				m_TypeID = typeID;
				m_TypeName = typeName;
				m_Position = pos;
				m_Length = length;
			}
		}

		private sealed class MobileEntry : IEntityEntry
		{
			private readonly Mobile m_Mobile;
			private readonly int m_TypeID;
			private readonly string m_TypeName;
			private readonly long m_Position;
			private readonly int m_Length;

			public Mobile Mobile => m_Mobile;

			public Serial Serial => m_Mobile == null ? Serial.MinusOne : m_Mobile.Serial;

			public int TypeID => m_TypeID;

			public string TypeName => m_TypeName;

			public long Position => m_Position;

			public int Length => m_Length;

			public MobileEntry(Mobile mobile, int typeID, string typeName, long pos, int length)
			{
				m_Mobile = mobile;
				m_TypeID = typeID;
				m_TypeName = typeName;
				m_Position = pos;
				m_Length = length;
			}
		}

		private static string m_LoadingType;

		public static string LoadingType => m_LoadingType;

		private static readonly Type[] m_SerialTypeArray = new Type[1] { typeof(Serial) };

		private static List<Tuple<ConstructorInfo, string>> ReadTypes(BinaryReader tdbReader)
		{
			var count = tdbReader.ReadInt32();

			var types = new List<Tuple<ConstructorInfo, string>>(count);

			for (var i = 0; i < count; ++i)
			{
				var typeName = tdbReader.ReadString();

				var t = ScriptCompiler.FindTypeByFullName(typeName);

				if (t == null)
				{
					Console.WriteLine("failed");

					if (!Core.Service)
					{
						Console.WriteLine("Error: Type '{0}' was not found. Delete all of those types? (y/n)", typeName);

						if (Console.ReadKey(true).Key == ConsoleKey.Y)
						{
							types.Add(null);
							Console.Write("World: Loading...");
							continue;
						}

						Console.WriteLine("Types will not be deleted. An exception will be thrown.");
					}
					else
					{
						Console.WriteLine("Error: Type '{0}' was not found.", typeName);
					}

					throw new Exception(String.Format("Bad type '{0}'", typeName));
				}

				var ctor = t.GetConstructor(m_SerialTypeArray);

				if (ctor != null)
				{
					types.Add(new Tuple<ConstructorInfo, string>(ctor, typeName));
				}
				else
				{
					throw new Exception(String.Format("Type '{0}' does not have a serialization constructor", t));
				}
			}

			return types;
		}

		public static void Load()
		{
			if (m_Loaded)
			{
				return;
			}

			m_Loaded = true;
			m_LoadingType = null;

			Console.Write("World: Loading...");

			var watch = Stopwatch.StartNew();

			m_Loading = true;

			_addQueue = new Queue<IEntity>();
			_deleteQueue = new Queue<IEntity>();

			int mobileCount = 0, itemCount = 0, guildCount = 0;

			var ctorArgs = new object[1];

			var items = new List<ItemEntry>();
			var mobiles = new List<MobileEntry>();
			var guilds = new List<GuildEntry>();

			if (File.Exists(MobileIndexPath) && File.Exists(MobileTypesPath))
			{
				using (var idx = new FileStream(MobileIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var idxReader = new BinaryReader(idx);

					using (var tdb = new FileStream(MobileTypesPath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						var tdbReader = new BinaryReader(tdb);

						var types = ReadTypes(tdbReader);

						mobileCount = idxReader.ReadInt32();

						m_Mobiles = new Dictionary<Serial, Mobile>(mobileCount);

						for (var i = 0; i < mobileCount; ++i)
						{
							var typeID = idxReader.ReadInt32();
							var serial = idxReader.ReadInt32();
							var pos = idxReader.ReadInt64();
							var length = idxReader.ReadInt32();

							var objs = types[typeID];

							if (objs == null)
							{
								continue;
							}

							Mobile m = null;
							var ctor = objs.Item1;
							var typeName = objs.Item2;

							try
							{
								ctorArgs[0] = (Serial)serial;
								m = (Mobile)(ctor.Invoke(ctorArgs));
							}
							catch
							{
							}

							if (m != null)
							{
								mobiles.Add(new MobileEntry(m, typeID, typeName, pos, length));
								AddMobile(m);
							}
						}

						tdbReader.Close();
					}

					idxReader.Close();
				}
			}
			else
			{
				m_Mobiles = new Dictionary<Serial, Mobile>();
			}

			if (File.Exists(ItemIndexPath) && File.Exists(ItemTypesPath))
			{
				using (var idx = new FileStream(ItemIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var idxReader = new BinaryReader(idx);

					using (var tdb = new FileStream(ItemTypesPath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						var tdbReader = new BinaryReader(tdb);

						var types = ReadTypes(tdbReader);

						itemCount = idxReader.ReadInt32();

						m_Items = new Dictionary<Serial, Item>(itemCount);

						for (var i = 0; i < itemCount; ++i)
						{
							var typeID = idxReader.ReadInt32();
							var serial = idxReader.ReadInt32();
							var pos = idxReader.ReadInt64();
							var length = idxReader.ReadInt32();

							var objs = types[typeID];

							if (objs == null)
							{
								continue;
							}

							Item item = null;
							var ctor = objs.Item1;
							var typeName = objs.Item2;

							try
							{
								ctorArgs[0] = (Serial)serial;
								item = (Item)(ctor.Invoke(ctorArgs));
							}
							catch
							{
							}

							if (item != null)
							{
								items.Add(new ItemEntry(item, typeID, typeName, pos, length));
								AddItem(item);
							}
						}

						tdbReader.Close();
					}

					idxReader.Close();
				}
			}
			else
			{
				m_Items = new Dictionary<Serial, Item>();
			}

			if (File.Exists(GuildIndexPath))
			{
				using (var idx = new FileStream(GuildIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var idxReader = new BinaryReader(idx);

					guildCount = idxReader.ReadInt32();

					var createEventArgs = new CreateGuildEventArgs(-1);
					for (var i = 0; i < guildCount; ++i)
					{
						idxReader.ReadInt32();//no typeid for guilds
						var id = idxReader.ReadInt32();
						var pos = idxReader.ReadInt64();
						var length = idxReader.ReadInt32();

						createEventArgs.Id = id;
						EventSink.InvokeCreateGuild(createEventArgs);
						var guild = createEventArgs.Guild;
						if (guild != null)
						{
							guilds.Add(new GuildEntry(guild, pos, length));
						}
					}

					idxReader.Close();
				}
			}

			bool failedMobiles = false, failedItems = false, failedGuilds = false;
			Type failedType = null;
			var failedSerial = Serial.Zero;
			Exception failed = null;
			var failedTypeID = 0;

			if (File.Exists(MobileDataPath))
			{
				using (var bin = new FileStream(MobileDataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var reader = new BinaryFileReader(new BinaryReader(bin));

					for (var i = 0; i < mobiles.Count; ++i)
					{
						var entry = mobiles[i];
						var m = entry.Mobile;

						if (m != null)
						{
							reader.Seek(entry.Position, SeekOrigin.Begin);

							try
							{
								m_LoadingType = entry.TypeName;
								m.Deserialize(reader);

								if (reader.Position != (entry.Position + entry.Length))
								{
									throw new Exception(String.Format("***** Bad serialize on {0} *****", m.GetType()));
								}
							}
							catch (Exception e)
							{
								mobiles.RemoveAt(i);

								failed = e;
								failedMobiles = true;
								failedType = m.GetType();
								failedTypeID = entry.TypeID;
								failedSerial = m.Serial;

								break;
							}
						}
					}

					reader.Close();
				}
			}

			if (!failedMobiles && File.Exists(ItemDataPath))
			{
				using (var bin = new FileStream(ItemDataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var reader = new BinaryFileReader(new BinaryReader(bin));

					for (var i = 0; i < items.Count; ++i)
					{
						var entry = items[i];
						var item = entry.Item;

						if (item != null)
						{
							reader.Seek(entry.Position, SeekOrigin.Begin);

							try
							{
								m_LoadingType = entry.TypeName;
								item.Deserialize(reader);

								if (reader.Position != (entry.Position + entry.Length))
								{
									throw new Exception(String.Format("***** Bad serialize on {0} *****", item.GetType()));
								}
							}
							catch (Exception e)
							{
								items.RemoveAt(i);

								failed = e;
								failedItems = true;
								failedType = item.GetType();
								failedTypeID = entry.TypeID;
								failedSerial = item.Serial;

								break;
							}
						}
					}

					reader.Close();
				}
			}

			m_LoadingType = null;

			if (!failedMobiles && !failedItems && File.Exists(GuildDataPath))
			{
				using (var bin = new FileStream(GuildDataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var reader = new BinaryFileReader(new BinaryReader(bin));

					for (var i = 0; i < guilds.Count; ++i)
					{
						var entry = guilds[i];
						var g = entry.Guild;

						if (g != null)
						{
							reader.Seek(entry.Position, SeekOrigin.Begin);

							try
							{
								g.Deserialize(reader);

								if (reader.Position != (entry.Position + entry.Length))
								{
									throw new Exception(String.Format("***** Bad serialize on Guild {0} *****", g.Id));
								}
							}
							catch (Exception e)
							{
								guilds.RemoveAt(i);

								failed = e;
								failedGuilds = true;
								failedType = typeof(BaseGuild);
								failedTypeID = g.Id;
								failedSerial = g.Id;

								break;
							}
						}
					}

					reader.Close();
				}
			}

			if (failedItems || failedMobiles || failedGuilds)
			{
				Console.WriteLine("An error was encountered while loading a saved object");

				Console.WriteLine(" - Type: {0}", failedType);
				Console.WriteLine(" - Serial: {0}", failedSerial);

				if (!Core.Service)
				{
					Console.WriteLine("Delete the object? (y/n)");

					if (Console.ReadKey(true).Key == ConsoleKey.Y)
					{
						if (failedType != typeof(BaseGuild))
						{
							Console.WriteLine("Delete all objects of that type? (y/n)");

							if (Console.ReadKey(true).Key == ConsoleKey.Y)
							{
								if (failedMobiles)
								{
									for (var i = 0; i < mobiles.Count;)
									{
										if (mobiles[i].TypeID == failedTypeID)
										{
											mobiles.RemoveAt(i);
										}
										else
										{
											++i;
										}
									}
								}
								else if (failedItems)
								{
									for (var i = 0; i < items.Count;)
									{
										if (items[i].TypeID == failedTypeID)
										{
											items.RemoveAt(i);
										}
										else
										{
											++i;
										}
									}
								}
							}
						}

						SaveIndex<MobileEntry>(mobiles, MobileIndexPath);
						SaveIndex<ItemEntry>(items, ItemIndexPath);
						SaveIndex<GuildEntry>(guilds, GuildIndexPath);
					}

					Console.WriteLine("After pressing return an exception will be thrown and the server will terminate.");
					Console.ReadLine();
				}
				else
				{
					Console.WriteLine("An exception will be thrown and the server will terminate.");
				}

				throw new Exception(String.Format("Load failed (items={0}, mobiles={1}, guilds={2}, type={3}, serial={4})", failedItems, failedMobiles, failedGuilds, failedType, failedSerial), failed);
			}

			EventSink.InvokeWorldLoad();

			m_Loading = false;

			ProcessSafetyQueues();

			foreach (var item in m_Items.Values)
			{
				if (item.Parent == null)
				{
					item.UpdateTotals();
				}

				item.ClearProperties();
			}

			foreach (var m in m_Mobiles.Values)
			{
				m.UpdateRegion(); // Is this really needed?
				m.UpdateTotals();

				m.ClearProperties();
			}

			watch.Stop();

			Console.WriteLine("done ({1} items, {2} mobiles) ({0:F2} seconds)", watch.Elapsed.TotalSeconds, m_Items.Count, m_Mobiles.Count);
		}

		private static void ProcessSafetyQueues()
		{
			while (_addQueue.Count > 0)
			{
				var entity = _addQueue.Dequeue();

				var item = entity as Item;

				if (item != null)
				{
					AddItem(item);
				}
				else
				{
					var mob = entity as Mobile;

					if (mob != null)
					{
						AddMobile(mob);
					}
				}
			}

			while (_deleteQueue.Count > 0)
			{
				var entity = _deleteQueue.Dequeue();

				var item = entity as Item;

				if (item != null)
				{
					item.Delete();
				}
				else
				{
					var mob = entity as Mobile;

					if (mob != null)
					{
						mob.Delete();
					}
				}
			}
		}

		private static void AppendSafetyLog(string action, IEntity entity)
		{
			var message = String.Format("Warning: Attempted to {1} {2} during world save." +
				"{0}This action could cause inconsistent state." +
				"{0}It is strongly advised that the offending scripts be corrected.",
				Environment.NewLine,
				action, entity
			);

			Console.WriteLine(message);

			try
			{
				using (var op = new StreamWriter("world-save-errors.log", true))
				{
					op.WriteLine("{0}\t{1}", DateTime.UtcNow, message);
					op.WriteLine(new StackTrace(2).ToString());
					op.WriteLine();
				}
			}
			catch
			{
			}
		}

		private static void SaveIndex<T>(List<T> list, string path) where T : IEntityEntry
		{
			if (!Directory.Exists("Export/Saves/Current/Mobiles/"))
			{
				Directory.CreateDirectory("Export/Saves/Current/Mobiles/");
			}

			if (!Directory.Exists("Export/Saves/Current/Items/"))
			{
				Directory.CreateDirectory("Export/Saves/Current/Items/");
			}

			if (!Directory.Exists("Export/Saves/Current/Guilds/"))
			{
				Directory.CreateDirectory("Export/Saves/Current/Guilds/");
			}

			using (var idx = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				var idxWriter = new BinaryWriter(idx);

				idxWriter.Write(list.Count);

				for (var i = 0; i < list.Count; ++i)
				{
					var e = list[i];

					idxWriter.Write(e.TypeID);
					idxWriter.Write(e.Serial);
					idxWriter.Write(e.Position);
					idxWriter.Write(e.Length);
				}

				idxWriter.Close();
			}
		}

		internal static int m_Saves;

		public static void Save()
		{
			Save(true, false);
		}

		public static void Save(bool message, bool permitBackgroundWrite)
		{
			if (m_Saving)
			{
				return;
			}

			++m_Saves;

			NetState.FlushAll();
			NetState.Pause();

			World.WaitForWriteCompletion();//Blocks Save until current disk flush is done.

			m_Saving = true;

			m_DiskWriteHandle.Reset();

			if (message)
			{
				Broadcast(0x35, true, "The world is saving, please wait.");
			}

			var strategy = SaveStrategy.Acquire();
			Console.WriteLine("Core: Using {0} save strategy", strategy.Name.ToLowerInvariant());

			Console.Write("World: Saving...");

			var watch = Stopwatch.StartNew();

			if (!Directory.Exists("Export/Saves/Current/Mobiles/"))
			{
				Directory.CreateDirectory("Export/Saves/Current/Mobiles/");
			}

			if (!Directory.Exists("Export/Saves/Current/Items/"))
			{
				Directory.CreateDirectory("Export/Saves/Current/Items/");
			}

			if (!Directory.Exists("Export/Saves/Current/Guilds/"))
			{
				Directory.CreateDirectory("Export/Saves/Current/Guilds/");
			}

			/*using ( SaveMetrics metrics = new SaveMetrics() ) {*/
			strategy.Save(null, permitBackgroundWrite);
			/*}*/

			try
			{
				EventSink.InvokeWorldSave(new WorldSaveEventArgs(message));
			}
			catch (Exception e)
			{
				throw new Exception("World Save event threw an exception.  Save failed!", e);
			}

			watch.Stop();

			m_Saving = false;

			if (!permitBackgroundWrite)
			{
				World.NotifyDiskWriteComplete();    //Sets the DiskWriteHandle.  If we allow background writes, we leave this upto the individual save strategies.
			}

			ProcessSafetyQueues();

			strategy.ProcessDecay();

			Console.WriteLine("Save done in {0:F2} seconds.", watch.Elapsed.TotalSeconds);

			if (message)
			{
				Broadcast(0x35, true, "World save complete. The entire process took {0:F1} seconds.", watch.Elapsed.TotalSeconds);
			}

			NetState.Resume();
		}

		internal static List<Type> m_ItemTypes = new List<Type>();
		internal static List<Type> m_MobileTypes = new List<Type>();

		public static IEntity FindEntity(Serial serial)
		{
			if (serial.IsItem)
			{
				return FindItem(serial);
			}
			else if (serial.IsMobile)
			{
				return FindMobile(serial);
			}

			return null;
		}

		public static Mobile FindMobile(Serial serial)
		{
			Mobile mob;

			m_Mobiles.TryGetValue(serial, out mob);

			return mob;
		}

		public static void AddMobile(Mobile m)
		{
			if (m_Saving)
			{
				AppendSafetyLog("add", m);
				_addQueue.Enqueue(m);
			}
			else
			{
				m_Mobiles[m.Serial] = m;
			}
		}

		public static Item FindItem(Serial serial)
		{
			Item item;

			m_Items.TryGetValue(serial, out item);

			return item;
		}

		public static void AddItem(Item item)
		{
			if (m_Saving)
			{
				AppendSafetyLog("add", item);
				_addQueue.Enqueue(item);
			}
			else
			{
				m_Items[item.Serial] = item;
			}
		}

		public static void RemoveMobile(Mobile m)
		{
			m_Mobiles.Remove(m.Serial);
		}

		public static void RemoveItem(Item item)
		{
			m_Items.Remove(item.Serial);
		}
	}

	#region Persistence

	public static class Persistence
	{
		public static void Serialize(string path, Action<GenericWriter> serializer)
		{
			Serialize(new FileInfo(path), serializer);
		}

		public static void Serialize(FileInfo file, Action<GenericWriter> serializer)
		{
			file.Refresh();

			if (file.Directory != null && !file.Directory.Exists)
			{
				file.Directory.Create();
			}

			if (!file.Exists)
			{
				file.Create().Close();
			}

			file.Refresh();

			using (var fs = file.OpenWrite())
			{
				var writer = new BinaryFileWriter(fs, true);

				try
				{
					serializer(writer);
				}
				finally
				{
					writer.Flush();
					writer.Close();
				}
			}
		}

		public static void Deserialize(string path, Action<GenericReader> deserializer)
		{
			Deserialize(path, deserializer, true);
		}

		public static void Deserialize(FileInfo file, Action<GenericReader> deserializer)
		{
			Deserialize(file, deserializer, true);
		}

		public static void Deserialize(string path, Action<GenericReader> deserializer, bool ensure)
		{
			Deserialize(new FileInfo(path), deserializer, ensure);
		}

		public static void Deserialize(FileInfo file, Action<GenericReader> deserializer, bool ensure)
		{
			file.Refresh();

			if (file.Directory != null && !file.Directory.Exists)
			{
				if (!ensure)
				{
					throw new DirectoryNotFoundException();
				}

				file.Directory.Create();
			}

			if (!file.Exists)
			{
				if (!ensure)
				{
					throw new FileNotFoundException
					{
						Source = file.FullName
					};
				}

				file.Create().Close();
			}

			file.Refresh();

			using (var fs = file.OpenRead())
			{
				var reader = new BinaryFileReader(new BinaryReader(fs));

				try
				{
					deserializer(reader);
				}
				catch (EndOfStreamException eos)
				{
					if (file.Length > 0)
					{
						Console.WriteLine("[Persistence]: {0}", eos);
					}
				}
				catch (Exception e)
				{
					Console.WriteLine("[Persistence]: {0}", e);
				}
				finally
				{
					reader.Close();
				}
			}
		}
	}

	public sealed class BinaryMemoryWriter : BinaryFileWriter
	{
		private readonly MemoryStream stream;

		protected override int BufferSize => 512;

		public BinaryMemoryWriter()
		 : base(new MemoryStream(512), true)
		{
			stream = UnderlyingStream as MemoryStream;
		}

		private static byte[] indexBuffer;

		public int CommitTo(SequentialFileWriter dataFile, SequentialFileWriter indexFile, int typeCode, int serial)
		{
			Flush();

			var buffer = stream.GetBuffer();
			var length = (int)stream.Length;

			var position = dataFile.Position;

			dataFile.Write(buffer, 0, length);

			if (indexBuffer == null)
			{
				indexBuffer = new byte[20];
			}

			indexBuffer[0] = (byte)(typeCode);
			indexBuffer[1] = (byte)(typeCode >> 8);
			indexBuffer[2] = (byte)(typeCode >> 16);
			indexBuffer[3] = (byte)(typeCode >> 24);

			indexBuffer[4] = (byte)(serial);
			indexBuffer[5] = (byte)(serial >> 8);
			indexBuffer[6] = (byte)(serial >> 16);
			indexBuffer[7] = (byte)(serial >> 24);

			indexBuffer[8] = (byte)(position);
			indexBuffer[9] = (byte)(position >> 8);
			indexBuffer[10] = (byte)(position >> 16);
			indexBuffer[11] = (byte)(position >> 24);
			indexBuffer[12] = (byte)(position >> 32);
			indexBuffer[13] = (byte)(position >> 40);
			indexBuffer[14] = (byte)(position >> 48);
			indexBuffer[15] = (byte)(position >> 56);

			indexBuffer[16] = (byte)(length);
			indexBuffer[17] = (byte)(length >> 8);
			indexBuffer[18] = (byte)(length >> 16);
			indexBuffer[19] = (byte)(length >> 24);

			indexFile.Write(indexBuffer, 0, indexBuffer.Length);

			stream.SetLength(0);

			return length;
		}
	}

	public sealed class QueuedMemoryWriter : BinaryFileWriter
	{
		private struct IndexInfo
		{
			public int size;
			public int typeCode;
			public int serial;
		}

		private readonly MemoryStream _memStream;
		private readonly List<IndexInfo> _orderedIndexInfo = new List<IndexInfo>();

		protected override int BufferSize => 512;

		public QueuedMemoryWriter()
			: base(new MemoryStream(1024 * 1024), true)
		{
			_memStream = UnderlyingStream as MemoryStream;
		}

		public void QueueForIndex(ISerializable serializable, int size)
		{
			IndexInfo info;

			info.size = size;

			info.typeCode = serializable.TypeReference; //For guilds, this will automagically be zero.
			info.serial = serializable.SerialIdentity;

			_orderedIndexInfo.Add(info);
		}

		public void CommitTo(SequentialFileWriter dataFile, SequentialFileWriter indexFile)
		{
			Flush();

			var memLength = (int)_memStream.Position;

			if (memLength > 0)
			{
				var memBuffer = _memStream.GetBuffer();

				var actualPosition = dataFile.Position;

				dataFile.Write(memBuffer, 0, memLength);    //The buffer contains the data from many items.

				//Console.WriteLine("Writing {0} bytes starting at {1}, with {2} things", memLength, actualPosition, _orderedIndexInfo.Count);

				var indexBuffer = new byte[20];

				//int indexWritten = _orderedIndexInfo.Count * indexBuffer.Length;
				//int totalWritten = memLength + indexWritten

				for (var i = 0; i < _orderedIndexInfo.Count; i++)
				{
					var info = _orderedIndexInfo[i];

					var typeCode = info.typeCode;
					var serial = info.serial;
					var length = info.size;


					indexBuffer[0] = (byte)(info.typeCode);
					indexBuffer[1] = (byte)(info.typeCode >> 8);
					indexBuffer[2] = (byte)(info.typeCode >> 16);
					indexBuffer[3] = (byte)(info.typeCode >> 24);

					indexBuffer[4] = (byte)(info.serial);
					indexBuffer[5] = (byte)(info.serial >> 8);
					indexBuffer[6] = (byte)(info.serial >> 16);
					indexBuffer[7] = (byte)(info.serial >> 24);

					indexBuffer[8] = (byte)(actualPosition);
					indexBuffer[9] = (byte)(actualPosition >> 8);
					indexBuffer[10] = (byte)(actualPosition >> 16);
					indexBuffer[11] = (byte)(actualPosition >> 24);
					indexBuffer[12] = (byte)(actualPosition >> 32);
					indexBuffer[13] = (byte)(actualPosition >> 40);
					indexBuffer[14] = (byte)(actualPosition >> 48);
					indexBuffer[15] = (byte)(actualPosition >> 56);

					indexBuffer[16] = (byte)(info.size);
					indexBuffer[17] = (byte)(info.size >> 8);
					indexBuffer[18] = (byte)(info.size >> 16);
					indexBuffer[19] = (byte)(info.size >> 24);

					indexFile.Write(indexBuffer, 0, indexBuffer.Length);

					actualPosition += info.size;
				}
			}

			Close();   //We're done with this writer.
		}
	}

	public delegate void FileCommitCallback(FileQueue.Chunk chunk);

	public sealed class FileQueue : IDisposable
	{
		public sealed class Chunk
		{
			private readonly FileQueue owner;
			private readonly int slot;

			private readonly byte[] buffer;
			private readonly int offset;
			private readonly int size;

			public byte[] Buffer => buffer;

			public int Offset => 0;

			public int Size => size;

			public Chunk(FileQueue owner, int slot, byte[] buffer, int offset, int size)
			{
				this.owner = owner;
				this.slot = slot;

				this.buffer = buffer;
				this.offset = offset;
				this.size = size;
			}

			public void Commit()
			{
				owner.Commit(this, slot);
			}
		}

		private struct Page
		{
			public byte[] buffer;
			public int length;
		}

		private static readonly int bufferSize;
		private static readonly BufferPool bufferPool;

		static FileQueue()
		{
			bufferSize = FileOperations.BufferSize;
			bufferPool = new BufferPool("File Buffers", 64, bufferSize);
		}

		private readonly object syncRoot;

		private readonly Chunk[] active;
		private int activeCount;

		private readonly Queue<Page> pending;
		private Page buffered;

		private readonly FileCommitCallback callback;

		private ManualResetEvent idle;

		private long position;

		public long Position => position;

		public FileQueue(int concurrentWrites, FileCommitCallback callback)
		{
			if (concurrentWrites < 1)
			{
				throw new ArgumentOutOfRangeException("concurrentWrites");
			}
			else if (bufferSize < 1)
			{
				throw new ArgumentOutOfRangeException("bufferSize");
			}
			else if (callback == null)
			{
				throw new ArgumentNullException("callback");
			}

			syncRoot = new object();

			active = new Chunk[concurrentWrites];
			pending = new Queue<Page>();

			this.callback = callback;

			idle = new ManualResetEvent(true);
		}

		private void Append(Page page)
		{
			lock (syncRoot)
			{
				if (activeCount == 0)
				{
					idle.Reset();
				}

				++activeCount;

				for (var slot = 0; slot < active.Length; ++slot)
				{
					if (active[slot] == null)
					{
						active[slot] = new Chunk(this, slot, page.buffer, 0, page.length);

						callback(active[slot]);

						return;
					}
				}

				pending.Enqueue(page);
			}
		}

		public void Dispose()
		{
			if (idle != null)
			{
				idle.Close();
				idle = null;
			}
		}

		public void Flush()
		{
			if (buffered.buffer != null)
			{
				Append(buffered);

				buffered.buffer = null;
				buffered.length = 0;
			}

			/*lock ( syncRoot ) {
				if ( pending.Count > 0 ) {
					idle.Reset();
				}

				for ( int slot = 0; slot < active.Length && pending.Count > 0; ++slot ) {
					if ( active[slot] == null ) {
						Page page = pending.Dequeue();

						active[slot] = new Chunk( this, slot, page.buffer, 0, page.length );

						++activeCount;

						callback( active[slot] );
					}
				}
			}*/

			idle.WaitOne();
		}

		private void Commit(Chunk chunk, int slot)
		{
			if (slot < 0 || slot >= active.Length)
			{
				throw new ArgumentOutOfRangeException("slot");
			}

			lock (syncRoot)
			{
				if (active[slot] != chunk)
				{
					throw new ArgumentException();
				}

				bufferPool.ReleaseBuffer(chunk.Buffer);

				if (pending.Count > 0)
				{
					var page = pending.Dequeue();

					active[slot] = new Chunk(this, slot, page.buffer, 0, page.length);

					callback(active[slot]);
				}
				else
				{
					active[slot] = null;
				}

				--activeCount;

				if (activeCount == 0)
				{
					idle.Set();
				}
			}
		}

		public void Enqueue(byte[] buffer, int offset, int size)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException("buffer");
			}
			else if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			else if (size < 0)
			{
				throw new ArgumentOutOfRangeException("size");
			}
			else if ((buffer.Length - offset) < size)
			{
				throw new ArgumentException();
			}

			position += size;

			while (size > 0)
			{
				if (buffered.buffer == null)
				{ // nothing yet buffered
					buffered.buffer = bufferPool.AcquireBuffer();
				}

				var page = buffered.buffer; // buffer page
				var pageSpace = page.Length - buffered.length; // available bytes in page
				var byteCount = (size > pageSpace ? pageSpace : size); // how many bytes we can copy over

				Buffer.BlockCopy(buffer, offset, page, buffered.length, byteCount);

				buffered.length += byteCount;
				offset += byteCount;
				size -= byteCount;

				if (buffered.length == page.Length)
				{ // page full
					Append(buffered);

					buffered.buffer = null;
					buffered.length = 0;
				}
			}
		}
	}

	public static class FileOperations
	{
		public const int KB = 1024;
		public const int MB = 1024 * KB;

#if !MONO
		private const FileOptions NoBuffering = (FileOptions)0x20000000;

		internal static class UnsafeNativeMethods
		{
			[DllImport("Kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
			internal static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, FileShare dwShareMode, IntPtr securityAttrs, FileMode dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);
		}
#endif

		private static int bufferSize = 1 * MB;
		private static int concurrency = 1;

		private static bool unbuffered = true;

		public static int BufferSize
		{
			get => bufferSize;
			set => bufferSize = value;
		}

		public static int Concurrency
		{
			get => concurrency;
			set => concurrency = value;
		}

		public static bool Unbuffered
		{
			get => unbuffered;
			set => unbuffered = value;
		}

		public static bool AreSynchronous => (concurrency < 1);

		public static bool AreAsynchronous => (concurrency > 0);

		public static FileStream OpenSequentialStream(string path, FileMode mode, FileAccess access, FileShare share)
		{
			var options = FileOptions.SequentialScan;

			if (concurrency > 0)
			{
				options |= FileOptions.Asynchronous;
			}

#if MONO
			return new FileStream( path, mode, access, share, bufferSize, options );
#else
			if (unbuffered)
			{
				options |= NoBuffering;
			}
			else
			{
				return new FileStream(path, mode, access, share, bufferSize, options);
			}

			var fileHandle = UnsafeNativeMethods.CreateFile(path, (int)access, share, IntPtr.Zero, mode, (int)options, IntPtr.Zero);

			if (fileHandle.IsInvalid)
			{
				throw new IOException();
			}

			return new UnbufferedFileStream(fileHandle, access, bufferSize, (concurrency > 0));
#endif
		}

#if !MONO
		private class UnbufferedFileStream : FileStream
		{
			private readonly SafeFileHandle fileHandle;

			public UnbufferedFileStream(SafeFileHandle fileHandle, FileAccess access, int bufferSize, bool isAsync)
			 : base(fileHandle, access, bufferSize, isAsync)
			{
				this.fileHandle = fileHandle;
			}

			public override void Write(byte[] array, int offset, int count)
			{
				base.Write(array, offset, bufferSize);
			}

			public override IAsyncResult BeginWrite(byte[] array, int offset, int numBytes, AsyncCallback userCallback, object stateObject)
			{
				return base.BeginWrite(array, offset, bufferSize, userCallback, stateObject);
			}

			protected override void Dispose(bool disposing)
			{
				if (!fileHandle.IsClosed)
				{
					fileHandle.Close();
				}

				base.Dispose(disposing);
			}
		}
#endif
	}

	public sealed class SequentialFileWriter : Stream
	{
		private FileStream fileStream;
		private FileQueue fileQueue;

		private AsyncCallback writeCallback;

		private readonly SaveMetrics metrics;

		public SequentialFileWriter(string path, SaveMetrics metrics)
		{
			if (path == null)
			{
				throw new ArgumentNullException("path");
			}

			this.metrics = metrics;

			fileStream = FileOperations.OpenSequentialStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

			fileQueue = new FileQueue(
				Math.Max(1, FileOperations.Concurrency),
				FileCallback
			);
		}

		public override long Position
		{
			get => fileQueue.Position;
			set => throw new InvalidOperationException();
		}

		private void FileCallback(FileQueue.Chunk chunk)
		{
			if (FileOperations.AreSynchronous)
			{
				fileStream.Write(chunk.Buffer, chunk.Offset, chunk.Size);

				if (metrics != null)
				{
					metrics.OnFileWritten(chunk.Size);
				}

				chunk.Commit();
			}
			else
			{
				if (writeCallback == null)
				{
					writeCallback = OnWrite;
				}

				fileStream.BeginWrite(chunk.Buffer, chunk.Offset, chunk.Size, writeCallback, chunk);
			}
		}

		private void OnWrite(IAsyncResult asyncResult)
		{
			var chunk = asyncResult.AsyncState as FileQueue.Chunk;

			fileStream.EndWrite(asyncResult);

			if (metrics != null)
			{
				metrics.OnFileWritten(chunk.Size);
			}

			chunk.Commit();
		}

		public override void Write(byte[] buffer, int offset, int size)
		{
			fileQueue.Enqueue(buffer, offset, size);
		}

		public override void Flush()
		{
			fileQueue.Flush();
			fileStream.Flush();
		}

		protected override void Dispose(bool disposing)
		{
			if (fileStream != null)
			{
				Flush();

				fileQueue.Dispose();
				fileQueue = null;

				fileStream.Close();
				fileStream = null;
			}

			base.Dispose(disposing);
		}

		public override bool CanRead => false;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => Position;

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new InvalidOperationException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new InvalidOperationException();
		}

		public override void SetLength(long value)
		{
			fileStream.SetLength(value);
		}
	}

	public sealed class SaveMetrics : IDisposable
	{
		private const string PerformanceCategoryName = "uoAvos";
		private const string PerformanceCategoryDesc = "Performance counters for uoAvos";

#if !MONO
		private readonly PerformanceCounter numberOfWorldSaves;

		private readonly PerformanceCounter itemsPerSecond;
		private readonly PerformanceCounter mobilesPerSecond;

		private readonly PerformanceCounter serializedBytesPerSecond;
		private readonly PerformanceCounter writtenBytesPerSecond;

		public SaveMetrics()
		{
			if (!PerformanceCounterCategory.Exists(PerformanceCategoryName))
			{
				var counters = new CounterCreationDataCollection {
					new CounterCreationData(
						"Save - Count",
						"Number of world saves.",
						PerformanceCounterType.NumberOfItems32
					),

					new CounterCreationData(
						"Save - Items/sec",
						"Number of items saved per second.",
						PerformanceCounterType.RateOfCountsPerSecond32
					),

					new CounterCreationData(
						"Save - Mobiles/sec",
						"Number of mobiles saved per second.",
						PerformanceCounterType.RateOfCountsPerSecond32
					),

					new CounterCreationData(
						"Save - Serialized bytes/sec",
						"Amount of world-save bytes serialized per second.",
						PerformanceCounterType.RateOfCountsPerSecond32
					),

					new CounterCreationData(
						"Save - Written bytes/sec",
						"Amount of world-save bytes written to disk per second.",
						PerformanceCounterType.RateOfCountsPerSecond32
					)
				};

				PerformanceCounterCategory.Create(PerformanceCategoryName, PerformanceCategoryDesc, PerformanceCounterCategoryType.SingleInstance, counters);
			}

			numberOfWorldSaves = new PerformanceCounter(PerformanceCategoryName, "Save - Count", false);

			itemsPerSecond = new PerformanceCounter(PerformanceCategoryName, "Save - Items/sec", false);
			mobilesPerSecond = new PerformanceCounter(PerformanceCategoryName, "Save - Mobiles/sec", false);

			serializedBytesPerSecond = new PerformanceCounter(PerformanceCategoryName, "Save - Serialized bytes/sec", false);
			writtenBytesPerSecond = new PerformanceCounter(PerformanceCategoryName, "Save - Written bytes/sec", false);

			// increment number of world saves
			numberOfWorldSaves.Increment();
		}

		public void OnItemSaved(int numberOfBytes)
		{
			itemsPerSecond.Increment();

			serializedBytesPerSecond.IncrementBy(numberOfBytes);
		}

		public void OnMobileSaved(int numberOfBytes)
		{
			mobilesPerSecond.Increment();

			serializedBytesPerSecond.IncrementBy(numberOfBytes);
		}

		public void OnGuildSaved(int numberOfBytes)
		{
			serializedBytesPerSecond.IncrementBy(numberOfBytes);
		}

		public void OnFileWritten(int numberOfBytes)
		{
			writtenBytesPerSecond.IncrementBy(numberOfBytes);
		}

		private bool isDisposed;

		public void Dispose()
		{
			if (!isDisposed)
			{
				isDisposed = true;

				numberOfWorldSaves.Dispose();

				itemsPerSecond.Dispose();
				mobilesPerSecond.Dispose();

				serializedBytesPerSecond.Dispose();
				writtenBytesPerSecond.Dispose();
			}
		}
#else
		public void Dispose()
		{ }

		public void OnItemSaved(int numberOfBytes)
		{ }

		public void OnMobileSaved(int numberOfBytes)
		{ }

		public void OnGuildSaved(int numberOfBytes)
		{ }

		public void OnFileWritten(int numberOfBytes)
		{ }
#endif
	}

	public abstract class SaveStrategy
	{
		public static SaveStrategy Acquire()
		{
			if (Core.MultiProcessor)
			{
				var processorCount = Core.ProcessorCount;

				if (processorCount > 2)
				{
					return new DualSaveStrategy(); // return new DynamicSaveStrategy(); (4.0 or return new ParallelSaveStrategy(processorCount); (2.0)
				}
				else
				{
					return new DualSaveStrategy();
				}
			}
			else
			{
				return new StandardSaveStrategy();
			}
		}

		public abstract string Name { get; }
		public abstract void Save(SaveMetrics metrics, bool permitBackgroundWrite);

		public abstract void ProcessDecay();
	}

	public class StandardSaveStrategy : SaveStrategy
	{
		public enum SaveOption
		{
			Normal,
			Threaded
		}

		public static SaveOption SaveType = SaveOption.Normal;

		public override string Name => "Standard";

		private readonly Queue<Item> _decayQueue;
		private bool _permitBackgroundWrite;

		public StandardSaveStrategy()
		{
			_decayQueue = new Queue<Item>();
		}

		protected bool PermitBackgroundWrite { get => _permitBackgroundWrite; set => _permitBackgroundWrite = value; }

		protected bool UseSequentialWriters => (StandardSaveStrategy.SaveType == SaveOption.Normal || !_permitBackgroundWrite);

		public override void Save(SaveMetrics metrics, bool permitBackgroundWrite)
		{
			_permitBackgroundWrite = permitBackgroundWrite;

			SaveMobiles(metrics);
			SaveItems(metrics);
			SaveGuilds(metrics);

			if (permitBackgroundWrite && UseSequentialWriters)  //If we're permitted to write in the background, but we don't anyways, then notify.
			{
				World.NotifyDiskWriteComplete();
			}
		}

		protected void SaveMobiles(SaveMetrics metrics)
		{
			var mobiles = World.Mobiles;

			GenericWriter idx;
			GenericWriter tdb;
			GenericWriter bin;

			if (UseSequentialWriters)
			{
				idx = new BinaryFileWriter(World.MobileIndexPath, false);
				tdb = new BinaryFileWriter(World.MobileTypesPath, false);
				bin = new BinaryFileWriter(World.MobileDataPath, true);
			}
			else
			{
				idx = new AsyncWriter(World.MobileIndexPath, false);
				tdb = new AsyncWriter(World.MobileTypesPath, false);
				bin = new AsyncWriter(World.MobileDataPath, true);
			}

			idx.Write(mobiles.Count);
			foreach (var m in mobiles.Values)
			{
				var start = bin.Position;

				idx.Write(m.m_TypeRef);
				idx.Write(m.Serial);
				idx.Write(start);

				m.Serialize(bin);

				if (metrics != null)
				{
					metrics.OnMobileSaved((int)(bin.Position - start));
				}

				idx.Write((int)(bin.Position - start));

				m.FreeCache();
			}

			tdb.Write(World.m_MobileTypes.Count);

			for (var i = 0; i < World.m_MobileTypes.Count; ++i)
			{
				tdb.Write(World.m_MobileTypes[i].FullName);
			}

			idx.Close();
			tdb.Close();
			bin.Close();
		}

		protected void SaveItems(SaveMetrics metrics)
		{
			var items = World.Items;

			GenericWriter idx;
			GenericWriter tdb;
			GenericWriter bin;

			if (UseSequentialWriters)
			{
				idx = new BinaryFileWriter(World.ItemIndexPath, false);
				tdb = new BinaryFileWriter(World.ItemTypesPath, false);
				bin = new BinaryFileWriter(World.ItemDataPath, true);
			}
			else
			{
				idx = new AsyncWriter(World.ItemIndexPath, false);
				tdb = new AsyncWriter(World.ItemTypesPath, false);
				bin = new AsyncWriter(World.ItemDataPath, true);
			}

			idx.Write(items.Count);

			var n = DateTime.UtcNow;

			foreach (var item in items.Values)
			{
				if (item.Decays && item.Parent == null && item.Map != Map.Internal && (item.LastMoved + item.DecayTime) <= n)
				{
					_decayQueue.Enqueue(item);
				}

				var start = bin.Position;

				idx.Write(item.m_TypeRef);
				idx.Write(item.Serial);
				idx.Write(start);

				item.Serialize(bin);

				if (metrics != null)
				{
					metrics.OnItemSaved((int)(bin.Position - start));
				}

				idx.Write((int)(bin.Position - start));

				item.FreeCache();
			}

			tdb.Write(World.m_ItemTypes.Count);
			for (var i = 0; i < World.m_ItemTypes.Count; ++i)
			{
				tdb.Write(World.m_ItemTypes[i].FullName);
			}

			idx.Close();
			tdb.Close();
			bin.Close();
		}

		protected void SaveGuilds(SaveMetrics metrics)
		{
			GenericWriter idx;
			GenericWriter bin;

			if (UseSequentialWriters)
			{
				idx = new BinaryFileWriter(World.GuildIndexPath, false);
				bin = new BinaryFileWriter(World.GuildDataPath, true);
			}
			else
			{
				idx = new AsyncWriter(World.GuildIndexPath, false);
				bin = new AsyncWriter(World.GuildDataPath, true);
			}

			idx.Write(BaseGuild.List.Count);
			foreach (var guild in BaseGuild.List.Values)
			{
				var start = bin.Position;

				idx.Write(0);//guilds have no typeid
				idx.Write(guild.Id);
				idx.Write(start);

				guild.Serialize(bin);

				if (metrics != null)
				{
					metrics.OnGuildSaved((int)(bin.Position - start));
				}

				idx.Write((int)(bin.Position - start));
			}

			idx.Close();
			bin.Close();
		}

		public override void ProcessDecay()
		{
			while (_decayQueue.Count > 0)
			{
				var item = _decayQueue.Dequeue();

				if (item.OnDecay())
				{
					item.Delete();
				}
			}
		}
	}

	public sealed class DualSaveStrategy : StandardSaveStrategy
	{
		public override string Name => "Dual";

		public DualSaveStrategy()
		{
		}

		public override void Save(SaveMetrics metrics, bool permitBackgroundWrite)
		{
			PermitBackgroundWrite = permitBackgroundWrite;

			var saveThread = new Thread(delegate ()
			{
				SaveItems(metrics);
			})
			{
				Name = "Item Save Subset"
			};
			saveThread.Start();

			SaveMobiles(metrics);
			SaveGuilds(metrics);

			saveThread.Join();

			if (permitBackgroundWrite && UseSequentialWriters)  //If we're permitted to write in the background, but we don't anyways, then notify.
			{
				World.NotifyDiskWriteComplete();
			}
		}
	}

	public sealed class ParallelSaveStrategy : SaveStrategy
	{
		public override string Name => "Parallel";

		private readonly int processorCount;

		public ParallelSaveStrategy(int processorCount)
		{
			this.processorCount = processorCount;

			_decayQueue = new Queue<Item>();
		}

		private int GetThreadCount()
		{
			return processorCount - 1;
		}

		private SaveMetrics metrics;

		private SequentialFileWriter itemData, itemIndex;
		private SequentialFileWriter mobileData, mobileIndex;
		private SequentialFileWriter guildData, guildIndex;

		private readonly Queue<Item> _decayQueue;

		private Consumer[] consumers;
		private int cycle;

		private bool finished;

		public override void Save(SaveMetrics metrics, bool permitBackgroundWrite)
		{
			this.metrics = metrics;

			OpenFiles();

			consumers = new Consumer[GetThreadCount()];

			for (var i = 0; i < consumers.Length; ++i)
			{
				consumers[i] = new Consumer(this, 256);
			}

			IEnumerable<ISerializable> collection = new Producer();

			foreach (var value in collection)
			{
				while (!Enqueue(value))
				{
					if (!Commit())
					{
						Thread.Sleep(0);
					}
				}
			}

			finished = true;

			SaveTypeDatabases();

			WaitHandle.WaitAll(
				Array.ConvertAll<Consumer, WaitHandle>(
					consumers,
					delegate (Consumer input)
					{
						return input.completionEvent;
					}
				)
			);

			Commit();

			CloseFiles();
		}

		public override void ProcessDecay()
		{
			while (_decayQueue.Count > 0)
			{
				var item = _decayQueue.Dequeue();

				if (item.OnDecay())
				{
					item.Delete();
				}
			}
		}

		private void SaveTypeDatabases()
		{
			SaveTypeDatabase(World.ItemTypesPath, World.m_ItemTypes);
			SaveTypeDatabase(World.MobileTypesPath, World.m_MobileTypes);
		}

		private void SaveTypeDatabase(string path, List<Type> types)
		{
			var bfw = new BinaryFileWriter(path, false);

			bfw.Write(types.Count);

			foreach (var type in types)
			{
				bfw.Write(type.FullName);
			}

			bfw.Flush();

			bfw.Close();
		}

		private void OpenFiles()
		{
			itemData = new SequentialFileWriter(World.ItemDataPath, metrics);
			itemIndex = new SequentialFileWriter(World.ItemIndexPath, metrics);

			mobileData = new SequentialFileWriter(World.MobileDataPath, metrics);
			mobileIndex = new SequentialFileWriter(World.MobileIndexPath, metrics);

			guildData = new SequentialFileWriter(World.GuildDataPath, metrics);
			guildIndex = new SequentialFileWriter(World.GuildIndexPath, metrics);

			WriteCount(itemIndex, World.Items.Count);
			WriteCount(mobileIndex, World.Mobiles.Count);
			WriteCount(guildIndex, BaseGuild.List.Count);
		}

		private void WriteCount(SequentialFileWriter indexFile, int count)
		{
			var buffer = new byte[4];

			buffer[0] = (byte)(count);
			buffer[1] = (byte)(count >> 8);
			buffer[2] = (byte)(count >> 16);
			buffer[3] = (byte)(count >> 24);

			indexFile.Write(buffer, 0, buffer.Length);
		}

		private void CloseFiles()
		{
			itemData.Close();
			itemIndex.Close();

			mobileData.Close();
			mobileIndex.Close();

			guildData.Close();
			guildIndex.Close();

			World.NotifyDiskWriteComplete();
		}

		private void OnSerialized(ConsumableEntry entry)
		{
			var value = entry.value;
			var writer = entry.writer;

			var item = value as Item;

			if (item != null)
			{
				Save(item, writer);
			}
			else
			{
				var mob = value as Mobile;

				if (mob != null)
				{
					Save(mob, writer);
				}
				else
				{
					var guild = value as BaseGuild;

					if (guild != null)
					{
						Save(guild, writer);
					}
				}
			}
		}

		private void Save(Item item, BinaryMemoryWriter writer)
		{
			var length = writer.CommitTo(itemData, itemIndex, item.m_TypeRef, item.Serial);

			if (metrics != null)
			{
				metrics.OnItemSaved(length);
			}

			if (item.Decays && item.Parent == null && item.Map != Map.Internal && DateTime.UtcNow > (item.LastMoved + item.DecayTime))
			{
				_decayQueue.Enqueue(item);
			}
		}

		private void Save(Mobile mob, BinaryMemoryWriter writer)
		{
			var length = writer.CommitTo(mobileData, mobileIndex, mob.m_TypeRef, mob.Serial);

			if (metrics != null)
			{
				metrics.OnMobileSaved(length);
			}
		}

		private void Save(BaseGuild guild, BinaryMemoryWriter writer)
		{
			var length = writer.CommitTo(guildData, guildIndex, 0, guild.Id);

			if (metrics != null)
			{
				metrics.OnGuildSaved(length);
			}
		}

		private bool Enqueue(ISerializable value)
		{
			for (var i = 0; i < consumers.Length; ++i)
			{
				var consumer = consumers[cycle++ % consumers.Length];

				if ((consumer.tail - consumer.head) < consumer.buffer.Length)
				{
					consumer.buffer[consumer.tail % consumer.buffer.Length].value = value;
					consumer.tail++;

					return true;
				}
			}

			return false;
		}

		private bool Commit()
		{
			var committed = false;

			for (var i = 0; i < consumers.Length; ++i)
			{
				var consumer = consumers[i];

				while (consumer.head < consumer.done)
				{
					OnSerialized(consumer.buffer[consumer.head % consumer.buffer.Length]);
					consumer.head++;

					committed = true;
				}
			}

			return committed;
		}

		private sealed class Producer : IEnumerable<ISerializable>
		{
			private readonly IEnumerable<Item> items;
			private readonly IEnumerable<Mobile> mobiles;
			private readonly IEnumerable<BaseGuild> guilds;

			public Producer()
			{
				items = World.Items.Values;
				mobiles = World.Mobiles.Values;
				guilds = BaseGuild.List.Values;
			}

			public IEnumerator<ISerializable> GetEnumerator()
			{
				foreach (var item in items)
				{
					yield return item;
				}

				foreach (var mob in mobiles)
				{
					yield return mob;
				}

				foreach (var guild in guilds)
				{
					yield return guild;
				}
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				throw new NotImplementedException();
			}
		}

		private struct ConsumableEntry
		{
			public ISerializable value;
			public BinaryMemoryWriter writer;
		}

		private sealed class Consumer
		{
			private readonly ParallelSaveStrategy owner;

			public ManualResetEvent completionEvent;

			public ConsumableEntry[] buffer;
			public int head, done, tail;

			private readonly Thread thread;

			public Consumer(ParallelSaveStrategy owner, int bufferSize)
			{
				this.owner = owner;

				buffer = new ConsumableEntry[bufferSize];

				for (var i = 0; i < buffer.Length; ++i)
				{
					buffer[i].writer = new BinaryMemoryWriter();
				}

				completionEvent = new ManualResetEvent(false);

				thread = new Thread(Processor)
				{
					Name = "Parallel Serialization Thread"
				};

				thread.Start();
			}

			private void Processor()
			{
				try
				{
					while (!owner.finished)
					{
						Process();
						Thread.Sleep(0);
					}

					Process();

					completionEvent.Set();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
			}

			private void Process()
			{
				ConsumableEntry entry;

				while (done < tail)
				{
					entry = buffer[done % buffer.Length];

					entry.value.Serialize(entry.writer);

					++done;
				}
			}
		}
	}

	public sealed class DynamicSaveStrategy : SaveStrategy
	{
		public override string Name => "Dynamic";

		private SaveMetrics _metrics;

		private SequentialFileWriter _itemData, _itemIndex;
		private SequentialFileWriter _mobileData, _mobileIndex;
		private SequentialFileWriter _guildData, _guildIndex;

		private readonly ConcurrentBag<Item> _decayBag;

		private readonly BlockingCollection<QueuedMemoryWriter> _itemThreadWriters;
		private readonly BlockingCollection<QueuedMemoryWriter> _mobileThreadWriters;
		private readonly BlockingCollection<QueuedMemoryWriter> _guildThreadWriters;

		public DynamicSaveStrategy()
		{
			_decayBag = new ConcurrentBag<Item>();
			_itemThreadWriters = new BlockingCollection<QueuedMemoryWriter>();
			_mobileThreadWriters = new BlockingCollection<QueuedMemoryWriter>();
			_guildThreadWriters = new BlockingCollection<QueuedMemoryWriter>();
		}

		public override void Save(SaveMetrics metrics, bool permitBackgroundWrite)
		{
			_metrics = metrics;

			OpenFiles();

			var saveTasks = new Task[3];

			saveTasks[0] = SaveItems();
			saveTasks[1] = SaveMobiles();
			saveTasks[2] = SaveGuilds();

			SaveTypeDatabases();

			if (permitBackgroundWrite)
			{
				//This option makes it finish the writing to disk in the background, continuing even after Save() returns.
				Task.Factory.ContinueWhenAll(saveTasks, _ =>
				{
					CloseFiles();

					World.NotifyDiskWriteComplete();
				});
			}
			else
			{
				Task.WaitAll(saveTasks);    //Waits for the completion of all of the tasks(committing to disk)
				CloseFiles();
			}
		}

		private Task StartCommitTask(BlockingCollection<QueuedMemoryWriter> threadWriter, SequentialFileWriter data, SequentialFileWriter index)
		{
			var commitTask = Task.Factory.StartNew(() =>
			{
				while (!(threadWriter.IsCompleted))
				{
					QueuedMemoryWriter writer;

					try
					{
						writer = threadWriter.Take();
					}
					catch (InvalidOperationException)
					{
						//Per MSDN, it's fine if we're here, successful completion of adding can rarely put us into this state.
						break;
					}

					writer.CommitTo(data, index);
				}
			});

			return commitTask;
		}

		private Task SaveItems()
		{
			//Start the blocking consumer; this runs in background.
			var commitTask = StartCommitTask(_itemThreadWriters, _itemData, _itemIndex);

			IEnumerable<Item> items = World.Items.Values;

			//Start the producer.
			Parallel.ForEach(items, () => new QueuedMemoryWriter(),
				(Item item, ParallelLoopState state, QueuedMemoryWriter writer) =>
				{
					var startPosition = writer.Position;

					item.Serialize(writer);

					var size = (int)(writer.Position - startPosition);

					writer.QueueForIndex(item, size);

					if (item.Decays && item.Parent == null && item.Map != Map.Internal && DateTime.UtcNow > (item.LastMoved + item.DecayTime))
					{
						_decayBag.Add(item);
					}

					if (_metrics != null)
					{
						_metrics.OnItemSaved(size);
					}

					return writer;
				},
				(writer) =>
				{
					writer.Flush();

					_itemThreadWriters.Add(writer);
				});

			_itemThreadWriters.CompleteAdding();    //We only get here after the Parallel.ForEach completes.  Lets our task 

			return commitTask;
		}

		private Task SaveMobiles()
		{
			//Start the blocking consumer; this runs in background.
			var commitTask = StartCommitTask(_mobileThreadWriters, _mobileData, _mobileIndex);

			IEnumerable<Mobile> mobiles = World.Mobiles.Values;

			//Start the producer.
			Parallel.ForEach(mobiles, () => new QueuedMemoryWriter(),
				(Mobile mobile, ParallelLoopState state, QueuedMemoryWriter writer) =>
				{
					var startPosition = writer.Position;

					mobile.Serialize(writer);

					var size = (int)(writer.Position - startPosition);

					writer.QueueForIndex(mobile, size);

					if (_metrics != null)
					{
						_metrics.OnMobileSaved(size);
					}

					return writer;
				},
				(writer) =>
				{
					writer.Flush();

					_mobileThreadWriters.Add(writer);
				});

			_mobileThreadWriters.CompleteAdding();  //We only get here after the Parallel.ForEach completes.  Lets our task tell the consumer that we're done

			return commitTask;
		}

		private Task SaveGuilds()
		{
			//Start the blocking consumer; this runs in background.
			var commitTask = StartCommitTask(_guildThreadWriters, _guildData, _guildIndex);

			IEnumerable<BaseGuild> guilds = BaseGuild.List.Values;

			//Start the producer.
			Parallel.ForEach(guilds, () => new QueuedMemoryWriter(),
				(BaseGuild guild, ParallelLoopState state, QueuedMemoryWriter writer) =>
				{
					var startPosition = writer.Position;

					guild.Serialize(writer);

					var size = (int)(writer.Position - startPosition);

					writer.QueueForIndex(guild, size);

					if (_metrics != null)
					{
						_metrics.OnGuildSaved(size);
					}

					return writer;
				},
				(writer) =>
				{
					writer.Flush();

					_guildThreadWriters.Add(writer);
				});

			_guildThreadWriters.CompleteAdding();   //We only get here after the Parallel.ForEach completes.  Lets our task 

			return commitTask;
		}

		public override void ProcessDecay()
		{
			Item item;

			while (_decayBag.TryTake(out item))
			{
				if (item.OnDecay())
				{
					item.Delete();
				}
			}
		}

		private void OpenFiles()
		{
			_itemData = new SequentialFileWriter(World.ItemDataPath, _metrics);
			_itemIndex = new SequentialFileWriter(World.ItemIndexPath, _metrics);

			_mobileData = new SequentialFileWriter(World.MobileDataPath, _metrics);
			_mobileIndex = new SequentialFileWriter(World.MobileIndexPath, _metrics);

			_guildData = new SequentialFileWriter(World.GuildDataPath, _metrics);
			_guildIndex = new SequentialFileWriter(World.GuildIndexPath, _metrics);

			WriteCount(_itemIndex, World.Items.Count);
			WriteCount(_mobileIndex, World.Mobiles.Count);
			WriteCount(_guildIndex, BaseGuild.List.Count);
		}

		private void CloseFiles()
		{
			_itemData.Close();
			_itemIndex.Close();

			_mobileData.Close();
			_mobileIndex.Close();

			_guildData.Close();
			_guildIndex.Close();
		}

		private void WriteCount(SequentialFileWriter indexFile, int count)
		{
			//Equiv to GenericWriter.Write( (int)count );
			var buffer = new byte[4];

			buffer[0] = (byte)(count);
			buffer[1] = (byte)(count >> 8);
			buffer[2] = (byte)(count >> 16);
			buffer[3] = (byte)(count >> 24);

			indexFile.Write(buffer, 0, buffer.Length);
		}

		private void SaveTypeDatabases()
		{
			SaveTypeDatabase(World.ItemTypesPath, World.m_ItemTypes);
			SaveTypeDatabase(World.MobileTypesPath, World.m_MobileTypes);
		}

		private void SaveTypeDatabase(string path, List<Type> types)
		{
			var bfw = new BinaryFileWriter(path, false);

			bfw.Write(types.Count);

			foreach (var type in types)
			{
				bfw.Write(type.FullName);
			}

			bfw.Flush();

			bfw.Close();
		}
	}

	#endregion
}