using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.GameContent.Creative;
using Terraria.GameContent.Events;
using Terraria.GameContent.Golf;
using Terraria.GameContent.Tile_Entities;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net;
using Terraria.Testing;
using Terraria.UI;

namespace Terraria;

public class MessageBuffer
{
	public const int readBufferMax = 131070;

	public const int writeBufferMax = 131070;

	public bool broadcast;

	public byte[] readBuffer = new byte[131070];

	public byte[] writeBuffer = new byte[131070];

	public bool writeLocked;

	public int messageLength;

	public int totalData;

	public int whoAmI;

	public int spamCount;

	public int maxSpam;

	public bool checkBytes;

	public MemoryStream readerStream;

	public MemoryStream writerStream;

	public BinaryReader reader;

	public BinaryWriter writer;

	public PacketHistory History = new PacketHistory();

	private float[] _temporaryProjectileAI = new float[Projectile.maxAI];

	private float[] _temporaryNPCAI = new float[NPC.maxAI];

	public static event TileChangeReceivedEvent OnTileChangeReceived;

	public void Reset()
	{
		Array.Clear(readBuffer, 0, readBuffer.Length);
		Array.Clear(writeBuffer, 0, writeBuffer.Length);
		writeLocked = false;
		messageLength = 0;
		totalData = 0;
		spamCount = 0;
		broadcast = false;
		checkBytes = false;
		ResetReader();
		ResetWriter();
	}

	public void ResetReader()
	{
		if (readerStream != null)
		{
			readerStream.Close();
		}
		readerStream = new MemoryStream(readBuffer);
		reader = new BinaryReader(readerStream);
	}

	public void ResetWriter()
	{
		if (writerStream != null)
		{
			writerStream.Close();
		}
		writerStream = new MemoryStream(writeBuffer);
		writer = new BinaryWriter(writerStream);
	}

	private float[] ReUseTemporaryProjectileAI()
	{
		for (int i = 0; i < _temporaryProjectileAI.Length; i++)
		{
			_temporaryProjectileAI[i] = 0f;
		}
		return _temporaryProjectileAI;
	}

	private float[] ReUseTemporaryNPCAI()
	{
		for (int i = 0; i < _temporaryNPCAI.Length; i++)
		{
			_temporaryNPCAI[i] = 0f;
		}
		return _temporaryNPCAI;
	}

	public void GetData(int start, int length, out int messageType)
	{
		if (whoAmI < 256)
		{
			Netplay.Clients[whoAmI].TimeOutTimer = 0;
		}
		else
		{
			Netplay.Connection.TimeOutTimer = 0;
		}
		byte flag = 0;
		int isActive = 0;
		isActive = start + 1;
		flag = (byte)(messageType = readBuffer[start]);
		if (flag >= MessageID.Count)
		{
			return;
		}
		Main.ActiveNetDiagnosticsUI.CountReadMessage(flag, length);
		if (Main.netMode == 1 && Netplay.Connection.StatusMax > 0)
		{
			Netplay.Connection.StatusCount++;
		}
		if (Main.verboseNetplay)
		{
			for (int isActive2 = start; isActive2 < start + length; isActive2++)
			{
			}
			for (int flag2 = start; flag2 < start + length; flag2++)
			{
				_ = readBuffer[flag2];
			}
		}
		if (Main.netMode == 2 && flag != 38 && Netplay.Clients[whoAmI].State == -1)
		{
			NetMessage.TrySendData(2, whoAmI, -1, Lang.mp[1].ToNetworkText());
			return;
		}
		if (Main.netMode == 2)
		{
			if (Netplay.Clients[whoAmI].State < 10 && flag > 12 && flag != 93 && flag != 16 && flag != 42 && flag != 50 && flag != 38 && flag != 68 && flag != 147)
			{
				NetMessage.BootPlayer(whoAmI, Lang.mp[2].ToNetworkText());
			}
			if (Netplay.Clients[whoAmI].State == 0 && flag != 1)
			{
				NetMessage.BootPlayer(whoAmI, Lang.mp[2].ToNetworkText());
			}
		}
		if (reader == null)
		{
			ResetReader();
		}
		reader.BaseStream.Position = isActive;
		switch (flag)
		{
		case 1:
			if (Main.netMode != 2)
			{
				break;
			}
			if (Main.dedServ && Netplay.IsBanned(Netplay.Clients[whoAmI].Socket.GetRemoteAddress()))
			{
				NetMessage.TrySendData(2, whoAmI, -1, Lang.mp[3].ToNetworkText());
			}
			else
			{
				if (Netplay.Clients[whoAmI].State != 0)
				{
					break;
				}
				if (reader.ReadString() == "Terraria" + 279)
				{
					if (string.IsNullOrEmpty(Netplay.ServerPassword))
					{
						Netplay.Clients[whoAmI].State = 1;
						NetMessage.TrySendData(3, whoAmI);
					}
					else
					{
						Netplay.Clients[whoAmI].State = -1;
						NetMessage.TrySendData(37, whoAmI);
					}
				}
				else
				{
					NetMessage.TrySendData(2, whoAmI, -1, Lang.mp[4].ToNetworkText());
				}
			}
			break;
		case 2:
			if (Main.netMode == 1)
			{
				Netplay.Disconnect = true;
				Main.statusText = NetworkText.Deserialize(reader).ToString();
			}
			break;
		case 3:
			if (Main.netMode == 1)
			{
				if (Netplay.Connection.State == 1)
				{
					Netplay.Connection.State = 2;
				}
				int flag3 = reader.ReadByte();
				bool num = reader.ReadBoolean();
				Netplay.Connection.ServerSpecialFlags[2] = num;
				if (flag3 != Main.myPlayer)
				{
					Main.player[flag3] = Main.ActivePlayerFileData.Player;
					Main.player[Main.myPlayer] = new Player();
				}
				Main.player[flag3].whoAmI = flag3;
				Main.myPlayer = flag3;
				Player scale = Main.player[flag3];
				NetMessage.TrySendData(4, -1, -1, null, flag3);
				NetMessage.TrySendData(68, -1, -1, null, flag3);
				NetMessage.TrySendData(16, -1, -1, null, flag3);
				NetMessage.TrySendData(42, -1, -1, null, flag3);
				NetMessage.TrySendData(50, -1, -1, null, flag3);
				NetMessage.TrySendData(147, -1, -1, null, flag3, scale.CurrentLoadoutIndex);
				for (int num12 = 0; num12 < 59; num12++)
				{
					NetMessage.TrySendData(5, -1, -1, null, flag3, PlayerItemSlotID.Inventory0 + num12, (int)scale.inventory[num12].prefix);
				}
				TrySendingItemArray(flag3, scale.armor, PlayerItemSlotID.Armor0);
				TrySendingItemArray(flag3, scale.dye, PlayerItemSlotID.Dye0);
				TrySendingItemArray(flag3, scale.miscEquips, PlayerItemSlotID.Misc0);
				TrySendingItemArray(flag3, scale.miscDyes, PlayerItemSlotID.MiscDye0);
				TrySendingItemArray(flag3, scale.bank.item, PlayerItemSlotID.Bank1_0);
				TrySendingItemArray(flag3, scale.bank2.item, PlayerItemSlotID.Bank2_0);
				NetMessage.TrySendData(5, -1, -1, null, flag3, PlayerItemSlotID.TrashItem, (int)scale.trashItem.prefix);
				TrySendingItemArray(flag3, scale.bank3.item, PlayerItemSlotID.Bank3_0);
				TrySendingItemArray(flag3, scale.bank4.item, PlayerItemSlotID.Bank4_0);
				TrySendingItemArray(flag3, scale.Loadouts[0].Armor, PlayerItemSlotID.Loadout1_Armor_0);
				TrySendingItemArray(flag3, scale.Loadouts[0].Dye, PlayerItemSlotID.Loadout1_Dye_0);
				TrySendingItemArray(flag3, scale.Loadouts[1].Armor, PlayerItemSlotID.Loadout2_Armor_0);
				TrySendingItemArray(flag3, scale.Loadouts[1].Dye, PlayerItemSlotID.Loadout2_Dye_0);
				TrySendingItemArray(flag3, scale.Loadouts[2].Armor, PlayerItemSlotID.Loadout3_Armor_0);
				TrySendingItemArray(flag3, scale.Loadouts[2].Dye, PlayerItemSlotID.Loadout3_Dye_0);
				NetMessage.TrySendData(6);
				if (Netplay.Connection.State == 2)
				{
					Netplay.Connection.State = 3;
				}
			}
			break;
		case 4:
		{
			int maxWidth = reader.ReadByte();
			if (Main.netMode == 2)
			{
				maxWidth = whoAmI;
			}
			if (maxWidth == Main.myPlayer && !Main.ServerSideCharacter)
			{
				break;
			}
			Player flag4 = Main.player[maxWidth];
			flag4.whoAmI = maxWidth;
			flag4.skinVariant = reader.ReadByte();
			flag4.skinVariant = (int)MathHelper.Clamp(flag4.skinVariant, 0f, PlayerVariantID.Count - 1);
			flag4.hair = reader.ReadByte();
			if (flag4.hair >= 165)
			{
				flag4.hair = 0;
			}
			flag4.name = reader.ReadString().Trim().Trim();
			flag4.hairDye = reader.ReadByte();
			ReadAccessoryVisibility(reader, flag4.hideVisibleAccessory);
			flag4.hideMisc = reader.ReadByte();
			flag4.hairColor = reader.ReadRGB();
			flag4.skinColor = reader.ReadRGB();
			flag4.eyeColor = reader.ReadRGB();
			flag4.shirtColor = reader.ReadRGB();
			flag4.underShirtColor = reader.ReadRGB();
			flag4.pantsColor = reader.ReadRGB();
			flag4.shoeColor = reader.ReadRGB();
			BitsByte value2 = reader.ReadByte();
			flag4.difficulty = 0;
			if (value2[0])
			{
				flag4.difficulty = 1;
			}
			if (value2[1])
			{
				flag4.difficulty = 2;
			}
			if (value2[3])
			{
				flag4.difficulty = 3;
			}
			if (flag4.difficulty > 3)
			{
				flag4.difficulty = 3;
			}
			flag4.extraAccessory = value2[2];
			BitsByte value3 = reader.ReadByte();
			flag4.UsingBiomeTorches = value3[0];
			flag4.happyFunTorchTime = value3[1];
			flag4.unlockedBiomeTorches = value3[2];
			flag4.unlockedSuperCart = value3[3];
			flag4.enabledSuperCart = value3[4];
			BitsByte num18 = reader.ReadByte();
			flag4.usedAegisCrystal = num18[0];
			flag4.usedAegisFruit = num18[1];
			flag4.usedArcaneCrystal = num18[2];
			flag4.usedGalaxyPearl = num18[3];
			flag4.usedGummyWorm = num18[4];
			flag4.usedAmbrosia = num18[5];
			flag4.ateArtisanBread = num18[6];
			if (Main.netMode != 2)
			{
				break;
			}
			bool num19 = false;
			if (Netplay.Clients[whoAmI].State < 10)
			{
				for (int num20 = 0; num20 < 255; num20++)
				{
					if (num20 != maxWidth && flag4.name == Main.player[num20].name && Netplay.Clients[num20].IsActive)
					{
						num19 = true;
					}
				}
			}
			if (num19)
			{
				NetMessage.TrySendData(2, whoAmI, -1, NetworkText.FromKey(Lang.mp[5].Key, flag4.name));
			}
			else if (flag4.name.Length > Player.nameLen)
			{
				NetMessage.TrySendData(2, whoAmI, -1, NetworkText.FromKey("Net.NameTooLong"));
			}
			else if (flag4.name == "")
			{
				NetMessage.TrySendData(2, whoAmI, -1, NetworkText.FromKey("Net.EmptyName"));
			}
			else if (flag4.difficulty == 3 && !Main.GameModeInfo.IsJourneyMode)
			{
				NetMessage.TrySendData(2, whoAmI, -1, NetworkText.FromKey("Net.PlayerIsCreativeAndWorldIsNotCreative"));
			}
			else if (flag4.difficulty != 3 && Main.GameModeInfo.IsJourneyMode)
			{
				NetMessage.TrySendData(2, whoAmI, -1, NetworkText.FromKey("Net.PlayerIsNotCreativeAndWorldIsCreative"));
			}
			else
			{
				Netplay.Clients[whoAmI].Name = flag4.name;
				Netplay.Clients[whoAmI].Name = flag4.name;
				NetMessage.TrySendData(4, -1, whoAmI, null, maxWidth);
			}
			break;
		}
		case 5:
		{
			int num21 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num21 = whoAmI;
			}
			if (num21 == Main.myPlayer && !Main.ServerSideCharacter && !Main.player[num21].HasLockedInventory())
			{
				break;
			}
			Player flag5 = Main.player[num21];
			lock (flag5)
			{
				int vector = reader.ReadInt16();
				int vector2 = reader.ReadInt16();
				int num2 = reader.ReadByte();
				int num3 = reader.ReadInt16();
				Item[] num4 = null;
				Item[] vector3 = null;
				int vector4 = 0;
				bool zero = false;
				Player i = Main.clientPlayer;
				if (vector >= PlayerItemSlotID.Loadout3_Dye_0)
				{
					vector4 = vector - PlayerItemSlotID.Loadout3_Dye_0;
					num4 = flag5.Loadouts[2].Dye;
					vector3 = i.Loadouts[2].Dye;
				}
				else if (vector >= PlayerItemSlotID.Loadout3_Armor_0)
				{
					vector4 = vector - PlayerItemSlotID.Loadout3_Armor_0;
					num4 = flag5.Loadouts[2].Armor;
					vector3 = i.Loadouts[2].Armor;
				}
				else if (vector >= PlayerItemSlotID.Loadout2_Dye_0)
				{
					vector4 = vector - PlayerItemSlotID.Loadout2_Dye_0;
					num4 = flag5.Loadouts[1].Dye;
					vector3 = i.Loadouts[1].Dye;
				}
				else if (vector >= PlayerItemSlotID.Loadout2_Armor_0)
				{
					vector4 = vector - PlayerItemSlotID.Loadout2_Armor_0;
					num4 = flag5.Loadouts[1].Armor;
					vector3 = i.Loadouts[1].Armor;
				}
				else if (vector >= PlayerItemSlotID.Loadout1_Dye_0)
				{
					vector4 = vector - PlayerItemSlotID.Loadout1_Dye_0;
					num4 = flag5.Loadouts[0].Dye;
					vector3 = i.Loadouts[0].Dye;
				}
				else if (vector >= PlayerItemSlotID.Loadout1_Armor_0)
				{
					vector4 = vector - PlayerItemSlotID.Loadout1_Armor_0;
					num4 = flag5.Loadouts[0].Armor;
					vector3 = i.Loadouts[0].Armor;
				}
				else if (vector >= PlayerItemSlotID.Bank4_0)
				{
					vector4 = vector - PlayerItemSlotID.Bank4_0;
					num4 = flag5.bank4.item;
					vector3 = i.bank4.item;
					if (Main.netMode == 1 && flag5.disableVoidBag == vector4)
					{
						flag5.disableVoidBag = -1;
						Recipe.FindRecipes(canDelayCheck: true);
					}
				}
				else if (vector >= PlayerItemSlotID.Bank3_0)
				{
					vector4 = vector - PlayerItemSlotID.Bank3_0;
					num4 = flag5.bank3.item;
					vector3 = i.bank3.item;
				}
				else if (vector >= PlayerItemSlotID.TrashItem)
				{
					zero = true;
				}
				else if (vector >= PlayerItemSlotID.Bank2_0)
				{
					vector4 = vector - PlayerItemSlotID.Bank2_0;
					num4 = flag5.bank2.item;
					vector3 = i.bank2.item;
				}
				else if (vector >= PlayerItemSlotID.Bank1_0)
				{
					vector4 = vector - PlayerItemSlotID.Bank1_0;
					num4 = flag5.bank.item;
					vector3 = i.bank.item;
				}
				else if (vector >= PlayerItemSlotID.MiscDye0)
				{
					vector4 = vector - PlayerItemSlotID.MiscDye0;
					num4 = flag5.miscDyes;
					vector3 = i.miscDyes;
				}
				else if (vector >= PlayerItemSlotID.Misc0)
				{
					vector4 = vector - PlayerItemSlotID.Misc0;
					num4 = flag5.miscEquips;
					vector3 = i.miscEquips;
				}
				else if (vector >= PlayerItemSlotID.Dye0)
				{
					vector4 = vector - PlayerItemSlotID.Dye0;
					num4 = flag5.dye;
					vector3 = i.dye;
				}
				else if (vector >= PlayerItemSlotID.Armor0)
				{
					vector4 = vector - PlayerItemSlotID.Armor0;
					num4 = flag5.armor;
					vector3 = i.armor;
				}
				else
				{
					vector4 = vector - PlayerItemSlotID.Inventory0;
					num4 = flag5.inventory;
					vector3 = i.inventory;
				}
				if (zero)
				{
					flag5.trashItem = new Item();
					flag5.trashItem.netDefaults(num3);
					flag5.trashItem.stack = vector2;
					flag5.trashItem.Prefix(num2);
					if (num21 == Main.myPlayer && !Main.ServerSideCharacter)
					{
						i.trashItem = flag5.trashItem.Clone();
					}
				}
				else if (vector <= 58)
				{
					int type16 = num4[vector4].type;
					int j = num4[vector4].stack;
					num4[vector4] = new Item();
					num4[vector4].netDefaults(num3);
					num4[vector4].stack = vector2;
					num4[vector4].Prefix(num2);
					if (num21 == Main.myPlayer && !Main.ServerSideCharacter)
					{
						vector3[vector4] = num4[vector4].Clone();
					}
					if (num21 == Main.myPlayer && vector4 == 58)
					{
						Main.mouseItem = num4[vector4].Clone();
					}
					if (num21 == Main.myPlayer && Main.netMode == 1)
					{
						Main.player[num21].inventoryChestStack[vector] = false;
						if (num4[vector4].stack != j || num4[vector4].type != type16)
						{
							Recipe.FindRecipes(canDelayCheck: true);
						}
					}
				}
				else
				{
					num4[vector4] = new Item();
					num4[vector4].netDefaults(num3);
					num4[vector4].stack = vector2;
					num4[vector4].Prefix(num2);
					if (num21 == Main.myPlayer && !Main.ServerSideCharacter)
					{
						vector3[vector4] = num4[vector4].Clone();
					}
				}
				bool[] canRelay = PlayerItemSlotID.CanRelay;
				if (Main.netMode == 2 && num21 == whoAmI && canRelay.IndexInRange(vector) && canRelay[vector])
				{
					NetMessage.TrySendData(5, -1, whoAmI, null, num21, vector, num2);
				}
				break;
			}
		}
		case 6:
			if (Main.netMode == 2)
			{
				if (Netplay.Clients[whoAmI].State == 1)
				{
					Netplay.Clients[whoAmI].State = 2;
				}
				NetMessage.TrySendData(7, whoAmI);
				Main.SyncAnInvasion(whoAmI);
			}
			break;
		case 7:
			if (Main.netMode == 1)
			{
				Main.time = reader.ReadInt32();
				BitsByte flag6 = reader.ReadByte();
				Main.dayTime = flag6[0];
				Main.bloodMoon = flag6[1];
				Main.eclipse = flag6[2];
				Main.moonPhase = reader.ReadByte();
				Main.maxTilesX = reader.ReadInt16();
				Main.maxTilesY = reader.ReadInt16();
				Main.spawnTileX = reader.ReadInt16();
				Main.spawnTileY = reader.ReadInt16();
				Main.worldSurface = reader.ReadInt16();
				Main.rockLayer = reader.ReadInt16();
				Main.worldID = reader.ReadInt32();
				Main.worldName = reader.ReadString();
				Main.GameMode = reader.ReadByte();
				Main.ActiveWorldFileData.UniqueId = new Guid(reader.ReadBytes(16));
				Main.ActiveWorldFileData.WorldGeneratorVersion = reader.ReadUInt64();
				Main.moonType = reader.ReadByte();
				WorldGen.setBG(0, reader.ReadByte());
				WorldGen.setBG(10, reader.ReadByte());
				WorldGen.setBG(11, reader.ReadByte());
				WorldGen.setBG(12, reader.ReadByte());
				WorldGen.setBG(1, reader.ReadByte());
				WorldGen.setBG(2, reader.ReadByte());
				WorldGen.setBG(3, reader.ReadByte());
				WorldGen.setBG(4, reader.ReadByte());
				WorldGen.setBG(5, reader.ReadByte());
				WorldGen.setBG(6, reader.ReadByte());
				WorldGen.setBG(7, reader.ReadByte());
				WorldGen.setBG(8, reader.ReadByte());
				WorldGen.setBG(9, reader.ReadByte());
				Main.iceBackStyle = reader.ReadByte();
				Main.jungleBackStyle = reader.ReadByte();
				Main.hellBackStyle = reader.ReadByte();
				Main.windSpeedTarget = reader.ReadSingle();
				Main.numClouds = reader.ReadByte();
				for (int flag7 = 0; flag7 < 3; flag7++)
				{
					Main.treeX[flag7] = reader.ReadInt32();
				}
				for (int text2 = 0; text2 < 4; text2++)
				{
					Main.treeStyle[text2] = reader.ReadByte();
				}
				for (int num7 = 0; num7 < 3; num7++)
				{
					Main.caveBackX[num7] = reader.ReadInt32();
				}
				for (int textValue = 0; textValue < 4; textValue++)
				{
					Main.caveBackStyle[textValue] = reader.ReadByte();
				}
				WorldGen.TreeTops.SyncReceive(reader);
				WorldGen.BackgroundsCache.UpdateCache();
				Main.maxRaining = reader.ReadSingle();
				Main.raining = Main.maxRaining > 0f;
				BitsByte value4 = reader.ReadByte();
				WorldGen.shadowOrbSmashed = value4[0];
				NPC.downedBoss1 = value4[1];
				NPC.downedBoss2 = value4[2];
				NPC.downedBoss3 = value4[3];
				Main.hardMode = value4[4];
				NPC.downedClown = value4[5];
				Main.ServerSideCharacter = value4[6];
				NPC.downedPlantBoss = value4[7];
				if (Main.ServerSideCharacter)
				{
					Main.ActivePlayerFileData.MarkAsServerSide();
				}
				BitsByte k = reader.ReadByte();
				NPC.downedMechBoss1 = k[0];
				NPC.downedMechBoss2 = k[1];
				NPC.downedMechBoss3 = k[2];
				NPC.downedMechBossAny = k[3];
				Main.cloudBGActive = (k[4] ? 1 : 0);
				WorldGen.crimson = k[5];
				Main.pumpkinMoon = k[6];
				Main.snowMoon = k[7];
				BitsByte bitsByte13 = reader.ReadByte();
				Main.fastForwardTimeToDawn = bitsByte13[1];
				Main.UpdateTimeRate();
				bool num197 = bitsByte13[2];
				NPC.downedSlimeKing = bitsByte13[3];
				NPC.downedQueenBee = bitsByte13[4];
				NPC.downedFishron = bitsByte13[5];
				NPC.downedMartians = bitsByte13[6];
				NPC.downedAncientCultist = bitsByte13[7];
				BitsByte l = reader.ReadByte();
				NPC.downedMoonlord = l[0];
				NPC.downedHalloweenKing = l[1];
				NPC.downedHalloweenTree = l[2];
				NPC.downedChristmasIceQueen = l[3];
				NPC.downedChristmasSantank = l[4];
				NPC.downedChristmasTree = l[5];
				NPC.downedGolemBoss = l[6];
				BirthdayParty.ManualParty = l[7];
				BitsByte num5 = reader.ReadByte();
				NPC.downedPirates = num5[0];
				NPC.downedFrost = num5[1];
				NPC.downedGoblins = num5[2];
				Sandstorm.Happening = num5[3];
				DD2Event.Ongoing = num5[4];
				DD2Event.DownedInvasionT1 = num5[5];
				DD2Event.DownedInvasionT2 = num5[6];
				DD2Event.DownedInvasionT3 = num5[7];
				BitsByte musicVolume = reader.ReadByte();
				NPC.combatBookWasUsed = musicVolume[0];
				LanternNight.ManualLanterns = musicVolume[1];
				NPC.downedTowerSolar = musicVolume[2];
				NPC.downedTowerVortex = musicVolume[3];
				NPC.downedTowerNebula = musicVolume[4];
				NPC.downedTowerStardust = musicVolume[5];
				Main.forceHalloweenForToday = musicVolume[6];
				Main.forceXMasForToday = musicVolume[7];
				BitsByte soundVolume = reader.ReadByte();
				NPC.boughtCat = soundVolume[0];
				NPC.boughtDog = soundVolume[1];
				NPC.boughtBunny = soundVolume[2];
				NPC.freeCake = soundVolume[3];
				Main.drunkWorld = soundVolume[4];
				NPC.downedEmpressOfLight = soundVolume[5];
				NPC.downedQueenSlime = soundVolume[6];
				Main.getGoodWorld = soundVolume[7];
				BitsByte ambientVolume = reader.ReadByte();
				Main.tenthAnniversaryWorld = ambientVolume[0];
				Main.dontStarveWorld = ambientVolume[1];
				NPC.downedDeerclops = ambientVolume[2];
				Main.notTheBeesWorld = ambientVolume[3];
				Main.remixWorld = ambientVolume[4];
				NPC.unlockedSlimeBlueSpawn = ambientVolume[5];
				NPC.combatBookVolumeTwoWasUsed = ambientVolume[6];
				NPC.peddlersSatchelWasUsed = ambientVolume[7];
				BitsByte text = reader.ReadByte();
				NPC.unlockedSlimeGreenSpawn = text[0];
				NPC.unlockedSlimeOldSpawn = text[1];
				NPC.unlockedSlimePurpleSpawn = text[2];
				NPC.unlockedSlimeRainbowSpawn = text[3];
				NPC.unlockedSlimeRedSpawn = text[4];
				NPC.unlockedSlimeYellowSpawn = text[5];
				NPC.unlockedSlimeCopperSpawn = text[6];
				Main.fastForwardTimeToDusk = text[7];
				BitsByte num6 = reader.ReadByte();
				Main.noTrapsWorld = num6[0];
				Main.zenithWorld = num6[1];
				NPC.unlockedTruffleSpawn = num6[2];
				Main.sundialCooldown = reader.ReadByte();
				Main.moondialCooldown = reader.ReadByte();
				WorldGen.SavedOreTiers.Copper = reader.ReadInt16();
				WorldGen.SavedOreTiers.Iron = reader.ReadInt16();
				WorldGen.SavedOreTiers.Silver = reader.ReadInt16();
				WorldGen.SavedOreTiers.Gold = reader.ReadInt16();
				WorldGen.SavedOreTiers.Cobalt = reader.ReadInt16();
				WorldGen.SavedOreTiers.Mythril = reader.ReadInt16();
				WorldGen.SavedOreTiers.Adamantite = reader.ReadInt16();
				if (num197)
				{
					Main.StartSlimeRain();
				}
				else
				{
					Main.StopSlimeRain();
				}
				Main.invasionType = reader.ReadSByte();
				Main.LobbyId = reader.ReadUInt64();
				Sandstorm.IntendedSeverity = reader.ReadSingle();
				if (Netplay.Connection.State == 3)
				{
					Main.windSpeedCurrent = Main.windSpeedTarget;
					Netplay.Connection.State = 4;
				}
				Main.checkHalloween();
				Main.checkXMas();
			}
			break;
		case 8:
		{
			if (Main.netMode != 2)
			{
				break;
			}
			NetMessage.TrySendData(7, whoAmI);
			int textValue2 = reader.ReadInt32();
			int num94 = reader.ReadInt32();
			bool flag10 = true;
			if (textValue2 == -1 || num94 == -1)
			{
				flag10 = false;
			}
			else if (textValue2 < 10 || textValue2 > Main.maxTilesX - 10)
			{
				flag10 = false;
			}
			else if (num94 < 10 || num94 > Main.maxTilesY - 10)
			{
				flag10 = false;
			}
			int num8 = Netplay.GetSectionX(Main.spawnTileX) - 2;
			int arg = Netplay.GetSectionY(Main.spawnTileY) - 1;
			int minimapFrame = num8 + 5;
			int text3 = arg + 3;
			if (num8 < 0)
			{
				num8 = 0;
			}
			if (minimapFrame >= Main.maxSectionsX)
			{
				minimapFrame = Main.maxSectionsX;
			}
			if (arg < 0)
			{
				arg = 0;
			}
			if (text3 >= Main.maxSectionsY)
			{
				text3 = Main.maxSectionsY;
			}
			int num9 = (minimapFrame - num8) * (text3 - arg);
			List<Point> arg2 = new List<Point>();
			for (int value5 = num8; value5 < minimapFrame; value5++)
			{
				for (int num95 = arg; num95 < text3; num95++)
				{
					arg2.Add(new Point(value5, num95));
				}
			}
			int playerResourcesDisplaySet = -1;
			int num96 = -1;
			if (flag10)
			{
				textValue2 = Netplay.GetSectionX(textValue2) - 2;
				num94 = Netplay.GetSectionY(num94) - 1;
				playerResourcesDisplaySet = textValue2 + 5;
				num96 = num94 + 3;
				if (textValue2 < 0)
				{
					textValue2 = 0;
				}
				if (playerResourcesDisplaySet >= Main.maxSectionsX)
				{
					playerResourcesDisplaySet = Main.maxSectionsX - 1;
				}
				if (num94 < 0)
				{
					num94 = 0;
				}
				if (num96 >= Main.maxSectionsY)
				{
					num96 = Main.maxSectionsY - 1;
				}
				for (int playerResourcesSet = textValue2; playerResourcesSet <= playerResourcesDisplaySet; playerResourcesSet++)
				{
					for (int value6 = num94; value6 <= num96; value6++)
					{
						if (playerResourcesSet < num8 || playerResourcesSet >= minimapFrame || value6 < arg || value6 >= text3)
						{
							arg2.Add(new Point(playerResourcesSet, value6));
							num9++;
						}
					}
				}
			}
			PortalHelper.SyncPortalsOnPlayerJoin(whoAmI, 1, arg2, out var minimapFrame2);
			num9 += minimapFrame2.Count;
			if (Netplay.Clients[whoAmI].State == 2)
			{
				Netplay.Clients[whoAmI].State = 3;
			}
			NetMessage.TrySendData(9, whoAmI, -1, Lang.inter[44].ToNetworkText(), num9);
			Netplay.Clients[whoAmI].StatusText2 = Language.GetTextValue("Net.IsReceivingTileData");
			Netplay.Clients[whoAmI].StatusMax += num9;
			for (int num10 = num8; num10 < minimapFrame; num10++)
			{
				for (int num13 = arg; num13 < text3; num13++)
				{
					NetMessage.SendSection(whoAmI, num10, num13);
				}
			}
			if (flag10)
			{
				for (int textValue3 = textValue2; textValue3 <= playerResourcesDisplaySet; textValue3++)
				{
					for (int num11 = num94; num11 <= num96; num11++)
					{
						NetMessage.SendSection(whoAmI, textValue3, num11);
					}
				}
			}
			for (int m = 0; m < minimapFrame2.Count; m++)
			{
				NetMessage.SendSection(whoAmI, minimapFrame2[m].X, minimapFrame2[m].Y);
			}
			for (int num97 = 0; num97 < 400; num97++)
			{
				if (Main.item[num97].active)
				{
					NetMessage.TrySendData(21, whoAmI, -1, null, num97);
					NetMessage.TrySendData(22, whoAmI, -1, null, num97);
				}
			}
			for (int num14 = 0; num14 < 200; num14++)
			{
				if (Main.npc[num14].active)
				{
					NetMessage.TrySendData(23, whoAmI, -1, null, num14);
				}
			}
			for (int num15 = 0; num15 < 1000; num15++)
			{
				if (Main.projectile[num15].active && (Main.projPet[Main.projectile[num15].type] || Main.projectile[num15].netImportant))
				{
					NetMessage.TrySendData(27, whoAmI, -1, null, num15);
				}
			}
			for (int hSLVector = 0; hSLVector < 290; hSLVector++)
			{
				NetMessage.TrySendData(83, whoAmI, -1, null, hSLVector);
			}
			NetMessage.TrySendData(57, whoAmI);
			NetMessage.TrySendData(103);
			NetMessage.TrySendData(101, whoAmI);
			NetMessage.TrySendData(136, whoAmI);
			NetMessage.TrySendData(49, whoAmI);
			Main.BestiaryTracker.OnPlayerJoining(whoAmI);
			CreativePowerManager.Instance.SyncThingsToJoiningPlayer(whoAmI);
			Main.PylonSystem.OnPlayerJoining(whoAmI);
			break;
		}
		case 9:
			if (Main.netMode == 1)
			{
				Netplay.Connection.StatusMax += reader.ReadInt32();
				Netplay.Connection.StatusText = NetworkText.Deserialize(reader).ToString();
				BitsByte x = reader.ReadByte();
				BitsByte num16 = Netplay.Connection.ServerSpecialFlags;
				num16[0] = x[0];
				num16[1] = x[1];
				Netplay.Connection.ServerSpecialFlags = num16;
			}
			break;
		case 10:
			if (Main.netMode == 1)
			{
				NetMessage.DecompressTileBlock(reader.BaseStream);
			}
			break;
		case 11:
			if (Main.netMode == 1)
			{
				WorldGen.SectionTileFrame(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
			}
			break;
		case 12:
		{
			int txt = reader.ReadByte();
			if (Main.netMode == 2)
			{
				txt = whoAmI;
			}
			Player player6 = Main.player[txt];
			player6.SpawnX = reader.ReadInt16();
			player6.SpawnY = reader.ReadInt16();
			player6.respawnTimer = reader.ReadInt32();
			player6.numberOfDeathsPVE = reader.ReadInt16();
			player6.numberOfDeathsPVP = reader.ReadInt16();
			if (player6.respawnTimer > 0)
			{
				player6.dead = true;
			}
			PlayerSpawnContext n = (PlayerSpawnContext)reader.ReadByte();
			player6.Spawn(n);
			if (Main.netMode != 2 || Netplay.Clients[whoAmI].State < 3)
			{
				break;
			}
			if (Netplay.Clients[whoAmI].State == 3)
			{
				Netplay.Clients[whoAmI].State = 10;
				NetMessage.buffer[whoAmI].broadcast = true;
				NetMessage.SyncConnectedPlayer(whoAmI);
				bool num17 = NetMessage.DoesPlayerSlotCountAsAHost(whoAmI);
				Main.countsAsHostForGameplay[whoAmI] = num17;
				if (NetMessage.DoesPlayerSlotCountAsAHost(whoAmI))
				{
					NetMessage.TrySendData(139, whoAmI, -1, null, whoAmI, num17.ToInt());
				}
				NetMessage.TrySendData(12, -1, whoAmI, null, whoAmI, (int)(byte)n);
				NetMessage.TrySendData(74, whoAmI, -1, NetworkText.FromLiteral(Main.player[whoAmI].name), Main.anglerQuest);
				NetMessage.TrySendData(129, whoAmI);
				NetMessage.greetPlayer(whoAmI);
				if (Main.player[txt].unlockedBiomeTorches)
				{
					NPC nPC2 = new NPC();
					nPC2.SetDefaults(664);
					Main.BestiaryTracker.Kills.RegisterKill(nPC2);
				}
			}
			else
			{
				NetMessage.TrySendData(12, -1, whoAmI, null, whoAmI, (int)(byte)n);
			}
			break;
		}
		case 13:
		{
			int num143 = reader.ReadByte();
			if (num143 != Main.myPlayer || Main.ServerSideCharacter)
			{
				if (Main.netMode == 2)
				{
					num143 = whoAmI;
				}
				Player player10 = Main.player[num143];
				BitsByte bitsByte7 = reader.ReadByte();
				BitsByte bitsByte8 = reader.ReadByte();
				BitsByte bitsByte9 = reader.ReadByte();
				BitsByte bitsByte10 = reader.ReadByte();
				player10.controlUp = bitsByte7[0];
				player10.controlDown = bitsByte7[1];
				player10.controlLeft = bitsByte7[2];
				player10.controlRight = bitsByte7[3];
				player10.controlJump = bitsByte7[4];
				player10.controlUseItem = bitsByte7[5];
				player10.direction = (bitsByte7[6] ? 1 : (-1));
				if (bitsByte8[0])
				{
					player10.pulley = true;
					player10.pulleyDir = (byte)((!bitsByte8[1]) ? 1u : 2u);
				}
				else
				{
					player10.pulley = false;
				}
				player10.vortexStealthActive = bitsByte8[3];
				player10.gravDir = (bitsByte8[4] ? 1 : (-1));
				player10.TryTogglingShield(bitsByte8[5]);
				player10.ghost = bitsByte8[6];
				player10.selectedItem = reader.ReadByte();
				player10.position = reader.ReadVector2();
				if (bitsByte8[2])
				{
					player10.velocity = reader.ReadVector2();
				}
				else
				{
					player10.velocity = Vector2.Zero;
				}
				if (bitsByte9[6])
				{
					player10.PotionOfReturnOriginalUsePosition = reader.ReadVector2();
					player10.PotionOfReturnHomePosition = reader.ReadVector2();
				}
				else
				{
					player10.PotionOfReturnOriginalUsePosition = null;
					player10.PotionOfReturnHomePosition = null;
				}
				player10.tryKeepingHoveringUp = bitsByte9[0];
				player10.IsVoidVaultEnabled = bitsByte9[1];
				player10.sitting.isSitting = bitsByte9[2];
				player10.downedDD2EventAnyDifficulty = bitsByte9[3];
				player10.isPettingAnimal = bitsByte9[4];
				player10.isTheAnimalBeingPetSmall = bitsByte9[5];
				player10.tryKeepingHoveringDown = bitsByte9[7];
				player10.sleeping.SetIsSleepingAndAdjustPlayerRotation(player10, bitsByte10[0]);
				player10.autoReuseAllWeapons = bitsByte10[1];
				player10.controlDownHold = bitsByte10[2];
				player10.isOperatingAnotherEntity = bitsByte10[3];
				player10.controlUseTile = bitsByte10[4];
				if (Main.netMode == 2 && Netplay.Clients[whoAmI].State == 10)
				{
					NetMessage.TrySendData(13, -1, whoAmI, null, num143);
				}
			}
			break;
		}
		case 14:
		{
			int num250 = reader.ReadByte();
			int num251 = reader.ReadByte();
			if (Main.netMode != 1)
			{
				break;
			}
			bool active = Main.player[num250].active;
			if (num251 == 1)
			{
				if (!Main.player[num250].active)
				{
					Main.player[num250] = new Player();
				}
				Main.player[num250].active = true;
			}
			else
			{
				Main.player[num250].active = false;
			}
			if (active != Main.player[num250].active)
			{
				if (Main.player[num250].active)
				{
					Player.Hooks.PlayerConnect(num250);
				}
				else
				{
					Player.Hooks.PlayerDisconnect(num250);
				}
			}
			break;
		}
		case 16:
		{
			int num144 = reader.ReadByte();
			if (num144 != Main.myPlayer || Main.ServerSideCharacter)
			{
				if (Main.netMode == 2)
				{
					num144 = whoAmI;
				}
				Player player11 = Main.player[num144];
				player11.statLife = reader.ReadInt16();
				player11.statLifeMax = reader.ReadInt16();
				if (player11.statLifeMax < 100)
				{
					player11.statLifeMax = 100;
				}
				player11.dead = player11.statLife <= 0;
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(16, -1, whoAmI, null, num144);
				}
			}
			break;
		}
		case 17:
		{
			byte b6 = reader.ReadByte();
			int num179 = reader.ReadInt16();
			int num180 = reader.ReadInt16();
			short num181 = reader.ReadInt16();
			int num182 = reader.ReadByte();
			bool flag15 = num181 == 1;
			if (!WorldGen.InWorld(num179, num180, 3))
			{
				break;
			}
			if (Main.tile[num179, num180] == null)
			{
				Main.tile[num179, num180] = new Tile();
			}
			if (Main.netMode == 2)
			{
				if (!flag15)
				{
					if (b6 == 0 || b6 == 2 || b6 == 4)
					{
						Netplay.Clients[whoAmI].SpamDeleteBlock += 1f;
					}
					if (b6 == 1 || b6 == 3)
					{
						Netplay.Clients[whoAmI].SpamAddBlock += 1f;
					}
				}
				if (!Netplay.Clients[whoAmI].TileSections[Netplay.GetSectionX(num179), Netplay.GetSectionY(num180)])
				{
					flag15 = true;
				}
			}
			if (b6 == 0)
			{
				WorldGen.KillTile(num179, num180, flag15);
				if (Main.netMode == 1 && !flag15)
				{
					HitTile.ClearAllTilesAtThisLocation(num179, num180);
				}
			}
			bool flag16 = false;
			if (b6 == 1)
			{
				bool forced = true;
				if (WorldGen.CheckTileBreakability2_ShouldTileSurvive(num179, num180))
				{
					flag16 = true;
					forced = false;
				}
				WorldGen.PlaceTile(num179, num180, num181, mute: false, forced, -1, num182);
			}
			if (b6 == 2)
			{
				WorldGen.KillWall(num179, num180, flag15);
			}
			if (b6 == 3)
			{
				WorldGen.PlaceWall(num179, num180, num181);
			}
			if (b6 == 4)
			{
				WorldGen.KillTile(num179, num180, flag15, effectOnly: false, noItem: true);
			}
			if (b6 == 5)
			{
				WorldGen.PlaceWire(num179, num180);
			}
			if (b6 == 6)
			{
				WorldGen.KillWire(num179, num180);
			}
			if (b6 == 7)
			{
				WorldGen.PoundTile(num179, num180);
			}
			if (b6 == 8)
			{
				WorldGen.PlaceActuator(num179, num180);
			}
			if (b6 == 9)
			{
				WorldGen.KillActuator(num179, num180);
			}
			if (b6 == 10)
			{
				WorldGen.PlaceWire2(num179, num180);
			}
			if (b6 == 11)
			{
				WorldGen.KillWire2(num179, num180);
			}
			if (b6 == 12)
			{
				WorldGen.PlaceWire3(num179, num180);
			}
			if (b6 == 13)
			{
				WorldGen.KillWire3(num179, num180);
			}
			if (b6 == 14)
			{
				WorldGen.SlopeTile(num179, num180, num181);
			}
			if (b6 == 15)
			{
				Minecart.FrameTrack(num179, num180, pound: true);
			}
			if (b6 == 16)
			{
				WorldGen.PlaceWire4(num179, num180);
			}
			if (b6 == 17)
			{
				WorldGen.KillWire4(num179, num180);
			}
			switch (b6)
			{
			case 18:
				Wiring.SetCurrentUser(whoAmI);
				Wiring.PokeLogicGate(num179, num180);
				Wiring.SetCurrentUser();
				return;
			case 19:
				Wiring.SetCurrentUser(whoAmI);
				Wiring.Actuate(num179, num180);
				Wiring.SetCurrentUser();
				return;
			case 20:
				if (WorldGen.InWorld(num179, num180, 2))
				{
					int type10 = Main.tile[num179, num180].type;
					WorldGen.KillTile(num179, num180, flag15);
					num181 = (short)((Main.tile[num179, num180].active() && Main.tile[num179, num180].type == type10) ? 1 : 0);
					if (Main.netMode == 2)
					{
						NetMessage.TrySendData(17, -1, -1, null, b6, num179, num180, num181, num182);
					}
				}
				return;
			case 21:
				WorldGen.ReplaceTile(num179, num180, (ushort)num181, num182);
				break;
			}
			if (b6 == 22)
			{
				WorldGen.ReplaceWall(num179, num180, (ushort)num181);
			}
			if (b6 == 23)
			{
				WorldGen.SlopeTile(num179, num180, num181);
				WorldGen.PoundTile(num179, num180);
			}
			if (Main.netMode == 2)
			{
				if (flag16)
				{
					NetMessage.SendTileSquare(-1, num179, num180, 5);
				}
				else if ((b6 != 1 && b6 != 21) || !TileID.Sets.Falling[num181] || Main.tile[num179, num180].active())
				{
					NetMessage.TrySendData(17, -1, whoAmI, null, b6, num179, num180, num181, num182);
				}
			}
			break;
		}
		case 18:
			if (Main.netMode == 1)
			{
				Main.dayTime = reader.ReadByte() == 1;
				Main.time = reader.ReadInt32();
				Main.sunModY = reader.ReadInt16();
				Main.moonModY = reader.ReadInt16();
			}
			break;
		case 19:
		{
			byte b10 = reader.ReadByte();
			int num198 = reader.ReadInt16();
			int num199 = reader.ReadInt16();
			if (WorldGen.InWorld(num198, num199, 3))
			{
				int num200 = ((reader.ReadByte() != 0) ? 1 : (-1));
				switch (b10)
				{
				case 0:
					WorldGen.OpenDoor(num198, num199, num200);
					break;
				case 1:
					WorldGen.CloseDoor(num198, num199, forced: true);
					break;
				case 2:
					WorldGen.ShiftTrapdoor(num198, num199, num200 == 1, 1);
					break;
				case 3:
					WorldGen.ShiftTrapdoor(num198, num199, num200 == 1, 0);
					break;
				case 4:
					WorldGen.ShiftTallGate(num198, num199, closing: false, forced: true);
					break;
				case 5:
					WorldGen.ShiftTallGate(num198, num199, closing: true, forced: true);
					break;
				}
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(19, -1, whoAmI, null, b10, num198, num199, (num200 == 1) ? 1 : 0);
				}
			}
			break;
		}
		case 20:
		{
			int num225 = reader.ReadInt16();
			int num226 = reader.ReadInt16();
			ushort num227 = reader.ReadByte();
			ushort num228 = reader.ReadByte();
			byte b12 = reader.ReadByte();
			if (!WorldGen.InWorld(num225, num226, 3))
			{
				break;
			}
			TileChangeType type12 = TileChangeType.None;
			if (Enum.IsDefined(typeof(TileChangeType), b12))
			{
				type12 = (TileChangeType)b12;
			}
			if (MessageBuffer.OnTileChangeReceived != null)
			{
				MessageBuffer.OnTileChangeReceived(num225, num226, Math.Max(num227, num228), type12);
			}
			BitsByte bitsByte15 = (byte)0;
			BitsByte bitsByte16 = (byte)0;
			BitsByte bitsByte17 = (byte)0;
			Tile tile4 = null;
			for (int num229 = num225; num229 < num225 + num227; num229++)
			{
				for (int num230 = num226; num230 < num226 + num228; num230++)
				{
					if (Main.tile[num229, num230] == null)
					{
						Main.tile[num229, num230] = new Tile();
					}
					tile4 = Main.tile[num229, num230];
					bool flag18 = tile4.active();
					bitsByte15 = reader.ReadByte();
					bitsByte16 = reader.ReadByte();
					bitsByte17 = reader.ReadByte();
					tile4.active(bitsByte15[0]);
					tile4.wall = (byte)(bitsByte15[2] ? 1u : 0u);
					bool flag19 = bitsByte15[3];
					if (Main.netMode != 2)
					{
						tile4.liquid = (byte)(flag19 ? 1u : 0u);
					}
					tile4.wire(bitsByte15[4]);
					tile4.halfBrick(bitsByte15[5]);
					tile4.actuator(bitsByte15[6]);
					tile4.inActive(bitsByte15[7]);
					tile4.wire2(bitsByte16[0]);
					tile4.wire3(bitsByte16[1]);
					if (bitsByte16[2])
					{
						tile4.color(reader.ReadByte());
					}
					if (bitsByte16[3])
					{
						tile4.wallColor(reader.ReadByte());
					}
					if (tile4.active())
					{
						int type13 = tile4.type;
						tile4.type = reader.ReadUInt16();
						if (Main.tileFrameImportant[tile4.type])
						{
							tile4.frameX = reader.ReadInt16();
							tile4.frameY = reader.ReadInt16();
						}
						else if (!flag18 || tile4.type != type13)
						{
							tile4.frameX = -1;
							tile4.frameY = -1;
						}
						byte b13 = 0;
						if (bitsByte16[4])
						{
							b13 = (byte)(b13 + 1);
						}
						if (bitsByte16[5])
						{
							b13 = (byte)(b13 + 2);
						}
						if (bitsByte16[6])
						{
							b13 = (byte)(b13 + 4);
						}
						tile4.slope(b13);
					}
					tile4.wire4(bitsByte16[7]);
					tile4.fullbrightBlock(bitsByte17[0]);
					tile4.fullbrightWall(bitsByte17[1]);
					tile4.invisibleBlock(bitsByte17[2]);
					tile4.invisibleWall(bitsByte17[3]);
					if (tile4.wall > 0)
					{
						tile4.wall = reader.ReadUInt16();
					}
					if (flag19)
					{
						tile4.liquid = reader.ReadByte();
						tile4.liquidType(reader.ReadByte());
					}
				}
			}
			WorldGen.RangeFrame(num225, num226, num225 + num227, num226 + num228);
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(flag, -1, whoAmI, null, num225, num226, (int)num227, (int)num228, b12);
			}
			break;
		}
		case 21:
		case 90:
		case 145:
		case 148:
		{
			int num110 = reader.ReadInt16();
			Vector2 position3 = reader.ReadVector2();
			Vector2 velocity3 = reader.ReadVector2();
			int stack3 = reader.ReadInt16();
			int prefixWeWant2 = reader.ReadByte();
			int num111 = reader.ReadByte();
			int num112 = reader.ReadInt16();
			bool shimmered = false;
			float shimmerTime = 0f;
			int timeLeftInWhichTheItemCannotBeTakenByEnemies = 0;
			if (flag == 145)
			{
				shimmered = reader.ReadBoolean();
				shimmerTime = reader.ReadSingle();
			}
			if (flag == 148)
			{
				timeLeftInWhichTheItemCannotBeTakenByEnemies = reader.ReadByte();
			}
			if (Main.netMode == 1)
			{
				if (num112 == 0)
				{
					Main.item[num110].active = false;
					break;
				}
				int num113 = num110;
				Item item2 = Main.item[num113];
				ItemSyncPersistentStats itemSyncPersistentStats = default(ItemSyncPersistentStats);
				itemSyncPersistentStats.CopyFrom(item2);
				bool newAndShiny = (item2.newAndShiny || item2.netID != num112) && ItemSlot.Options.HighlightNewItems && (num112 < 0 || num112 >= ItemID.Count || !ItemID.Sets.NeverAppearsAsNewInInventory[num112]);
				item2.netDefaults(num112);
				item2.newAndShiny = newAndShiny;
				item2.Prefix(prefixWeWant2);
				item2.stack = stack3;
				item2.position = position3;
				item2.velocity = velocity3;
				item2.active = true;
				item2.shimmered = shimmered;
				item2.shimmerTime = shimmerTime;
				if (flag == 90)
				{
					item2.instanced = true;
					item2.playerIndexTheItemIsReservedFor = Main.myPlayer;
					item2.keepTime = 600;
				}
				item2.timeLeftInWhichTheItemCannotBeTakenByEnemies = timeLeftInWhichTheItemCannotBeTakenByEnemies;
				item2.wet = Collision.WetCollision(item2.position, item2.width, item2.height);
				itemSyncPersistentStats.PasteInto(item2);
			}
			else
			{
				if (Main.timeItemSlotCannotBeReusedFor[num110] > 0)
				{
					break;
				}
				if (num112 == 0)
				{
					if (num110 < 400)
					{
						Main.item[num110].active = false;
						NetMessage.TrySendData(21, -1, -1, null, num110);
					}
					break;
				}
				bool flag12 = false;
				if (num110 == 400)
				{
					flag12 = true;
				}
				if (flag12)
				{
					Item item3 = new Item();
					item3.netDefaults(num112);
					num110 = Item.NewItem(new EntitySource_Sync(), (int)position3.X, (int)position3.Y, item3.width, item3.height, item3.type, stack3, noBroadcast: true);
				}
				Item item4 = Main.item[num110];
				item4.netDefaults(num112);
				item4.Prefix(prefixWeWant2);
				item4.stack = stack3;
				item4.position = position3;
				item4.velocity = velocity3;
				item4.active = true;
				item4.playerIndexTheItemIsReservedFor = Main.myPlayer;
				item4.timeLeftInWhichTheItemCannotBeTakenByEnemies = timeLeftInWhichTheItemCannotBeTakenByEnemies;
				if (flag == 145)
				{
					item4.shimmered = shimmered;
					item4.shimmerTime = shimmerTime;
				}
				if (flag12)
				{
					NetMessage.TrySendData(flag, -1, -1, null, num110);
					if (num111 == 0)
					{
						Main.item[num110].ownIgnore = whoAmI;
						Main.item[num110].ownTime = 100;
					}
					Main.item[num110].FindOwner(num110);
				}
				else
				{
					NetMessage.TrySendData(flag, -1, whoAmI, null, num110);
				}
			}
			break;
		}
		case 22:
		{
			int num66 = reader.ReadInt16();
			int num67 = reader.ReadByte();
			if (Main.netMode != 2 || Main.item[num66].playerIndexTheItemIsReservedFor == whoAmI)
			{
				Main.item[num66].playerIndexTheItemIsReservedFor = num67;
				if (num67 == Main.myPlayer)
				{
					Main.item[num66].keepTime = 15;
				}
				else
				{
					Main.item[num66].keepTime = 0;
				}
				if (Main.netMode == 2)
				{
					Main.item[num66].playerIndexTheItemIsReservedFor = 255;
					Main.item[num66].keepTime = 15;
					NetMessage.TrySendData(22, -1, -1, null, num66);
				}
			}
			break;
		}
		case 23:
		{
			if (Main.netMode != 1)
			{
				break;
			}
			int num158 = reader.ReadInt16();
			Vector2 vector9 = reader.ReadVector2();
			Vector2 velocity5 = reader.ReadVector2();
			int num159 = reader.ReadUInt16();
			if (num159 == 65535)
			{
				num159 = 0;
			}
			BitsByte bitsByte11 = reader.ReadByte();
			BitsByte bitsByte12 = reader.ReadByte();
			float[] array2 = ReUseTemporaryNPCAI();
			for (int num160 = 0; num160 < NPC.maxAI; num160++)
			{
				if (bitsByte11[num160 + 2])
				{
					array2[num160] = reader.ReadSingle();
				}
				else
				{
					array2[num160] = 0f;
				}
			}
			int num161 = reader.ReadInt16();
			int? playerCountForMultiplayerDifficultyOverride = 1;
			if (bitsByte12[0])
			{
				playerCountForMultiplayerDifficultyOverride = reader.ReadByte();
			}
			float value13 = 1f;
			if (bitsByte12[2])
			{
				value13 = reader.ReadSingle();
			}
			int num162 = 0;
			if (!bitsByte11[7])
			{
				num162 = reader.ReadByte() switch
				{
					2 => reader.ReadInt16(), 
					4 => reader.ReadInt32(), 
					_ => reader.ReadSByte(), 
				};
			}
			int num163 = -1;
			NPC nPC5 = Main.npc[num158];
			if (nPC5.active && Main.multiplayerNPCSmoothingRange > 0 && Vector2.DistanceSquared(nPC5.position, vector9) < 640000f)
			{
				nPC5.netOffset += nPC5.position - vector9;
			}
			if (!nPC5.active || nPC5.netID != num161)
			{
				nPC5.netOffset *= 0f;
				if (nPC5.active)
				{
					num163 = nPC5.type;
				}
				nPC5.active = true;
				nPC5.SetDefaults(num161, new NPCSpawnParams
				{
					playerCountForMultiplayerDifficultyOverride = playerCountForMultiplayerDifficultyOverride,
					strengthMultiplierOverride = value13
				});
			}
			nPC5.position = vector9;
			nPC5.velocity = velocity5;
			nPC5.target = num159;
			nPC5.direction = (bitsByte11[0] ? 1 : (-1));
			nPC5.directionY = (bitsByte11[1] ? 1 : (-1));
			nPC5.spriteDirection = (bitsByte11[6] ? 1 : (-1));
			if (bitsByte11[7])
			{
				num162 = (nPC5.life = nPC5.lifeMax);
			}
			else
			{
				nPC5.life = num162;
			}
			if (num162 <= 0)
			{
				nPC5.active = false;
			}
			nPC5.SpawnedFromStatue = bitsByte12[1];
			if (nPC5.SpawnedFromStatue)
			{
				nPC5.value = 0f;
			}
			for (int num164 = 0; num164 < NPC.maxAI; num164++)
			{
				nPC5.ai[num164] = array2[num164];
			}
			if (num163 > -1 && num163 != nPC5.type)
			{
				nPC5.TransformVisuals(num163, nPC5.type);
			}
			if (num161 == 262)
			{
				NPC.plantBoss = num158;
			}
			if (num161 == 245)
			{
				NPC.golemBoss = num158;
			}
			if (num161 == 668)
			{
				NPC.deerclopsBoss = num158;
			}
			if (nPC5.type >= 0 && nPC5.type < NPCID.Count && Main.npcCatchable[nPC5.type])
			{
				nPC5.releaseOwner = reader.ReadByte();
			}
			break;
		}
		case 24:
		{
			int num125 = reader.ReadInt16();
			int num126 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num126 = whoAmI;
			}
			Player player9 = Main.player[num126];
			Main.npc[num125].StrikeNPC(player9.inventory[player9.selectedItem].damage, player9.inventory[player9.selectedItem].knockBack, player9.direction);
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(24, -1, whoAmI, null, num125, num126);
				NetMessage.TrySendData(23, -1, -1, null, num125);
			}
			break;
		}
		case 27:
		{
			int num57 = reader.ReadInt16();
			Vector2 position = reader.ReadVector2();
			Vector2 velocity2 = reader.ReadVector2();
			int num58 = reader.ReadByte();
			int num59 = reader.ReadInt16();
			BitsByte bitsByte2 = reader.ReadByte();
			BitsByte bitsByte3 = (byte)(bitsByte2[2] ? reader.ReadByte() : 0);
			float[] array = ReUseTemporaryProjectileAI();
			array[0] = (bitsByte2[0] ? reader.ReadSingle() : 0f);
			array[1] = (bitsByte2[1] ? reader.ReadSingle() : 0f);
			int bannerIdToRespondTo = (bitsByte2[3] ? reader.ReadUInt16() : 0);
			int damage2 = (bitsByte2[4] ? reader.ReadInt16() : 0);
			float knockBack2 = (bitsByte2[5] ? reader.ReadSingle() : 0f);
			int originalDamage = (bitsByte2[6] ? reader.ReadInt16() : 0);
			int num60 = (bitsByte2[7] ? reader.ReadInt16() : (-1));
			if (num60 >= 1000)
			{
				num60 = -1;
			}
			array[2] = (bitsByte3[0] ? reader.ReadSingle() : 0f);
			if (Main.netMode == 2)
			{
				if (num59 == 949)
				{
					num58 = 255;
				}
				else
				{
					num58 = whoAmI;
					if (Main.projHostile[num59])
					{
						break;
					}
				}
			}
			int num61 = 1000;
			for (int num62 = 0; num62 < 1000; num62++)
			{
				if (Main.projectile[num62].owner == num58 && Main.projectile[num62].identity == num57 && Main.projectile[num62].active)
				{
					num61 = num62;
					break;
				}
			}
			if (num61 == 1000)
			{
				for (int num63 = 0; num63 < 1000; num63++)
				{
					if (!Main.projectile[num63].active)
					{
						num61 = num63;
						break;
					}
				}
			}
			if (num61 == 1000)
			{
				num61 = Projectile.FindOldestProjectile();
			}
			Projectile projectile = Main.projectile[num61];
			if (!projectile.active || projectile.type != num59)
			{
				projectile.SetDefaults(num59);
				if (Main.netMode == 2)
				{
					Netplay.Clients[whoAmI].SpamProjectile += 1f;
				}
			}
			projectile.identity = num57;
			projectile.position = position;
			projectile.velocity = velocity2;
			projectile.type = num59;
			projectile.damage = damage2;
			projectile.bannerIdToRespondTo = bannerIdToRespondTo;
			projectile.originalDamage = originalDamage;
			projectile.knockBack = knockBack2;
			projectile.owner = num58;
			for (int num64 = 0; num64 < Projectile.maxAI; num64++)
			{
				projectile.ai[num64] = array[num64];
			}
			if (num60 >= 0)
			{
				projectile.projUUID = num60;
				Main.projectileIdentity[num58, num60] = num61;
			}
			projectile.ProjectileFixDesperation();
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(27, -1, whoAmI, null, num61);
			}
			break;
		}
		case 28:
		{
			int num193 = reader.ReadInt16();
			int num194 = reader.ReadInt16();
			float num195 = reader.ReadSingle();
			int num196 = reader.ReadByte() - 1;
			byte b9 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				if (num194 < 0)
				{
					num194 = 0;
				}
				Main.npc[num193].PlayerInteraction(whoAmI);
			}
			if (num194 >= 0)
			{
				Main.npc[num193].StrikeNPC(num194, num195, num196, b9 == 1, noEffect: false, fromNet: true);
			}
			else
			{
				Main.npc[num193].life = 0;
				Main.npc[num193].HitEffect();
				Main.npc[num193].active = false;
			}
			if (Main.netMode != 2)
			{
				break;
			}
			NetMessage.TrySendData(28, -1, whoAmI, null, num193, num194, num195, num196, b9);
			if (Main.npc[num193].life <= 0)
			{
				NetMessage.TrySendData(23, -1, -1, null, num193);
			}
			else
			{
				Main.npc[num193].netUpdate = true;
			}
			if (Main.npc[num193].realLife >= 0)
			{
				if (Main.npc[Main.npc[num193].realLife].life <= 0)
				{
					NetMessage.TrySendData(23, -1, -1, null, Main.npc[num193].realLife);
				}
				else
				{
					Main.npc[Main.npc[num193].realLife].netUpdate = true;
				}
			}
			break;
		}
		case 29:
		{
			int num103 = reader.ReadInt16();
			int num104 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num104 = whoAmI;
			}
			for (int num105 = 0; num105 < 1000; num105++)
			{
				if (Main.projectile[num105].owner == num104 && Main.projectile[num105].identity == num103 && Main.projectile[num105].active)
				{
					Main.projectile[num105].Kill();
					break;
				}
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(29, -1, whoAmI, null, num103, num104);
			}
			break;
		}
		case 30:
		{
			int num142 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num142 = whoAmI;
			}
			bool flag14 = reader.ReadBoolean();
			Main.player[num142].hostile = flag14;
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(30, -1, whoAmI, null, num142);
				LocalizedText obj4 = (flag14 ? Lang.mp[11] : Lang.mp[12]);
				ChatHelper.BroadcastChatMessage(color: Main.teamColor[Main.player[num142].team], text: NetworkText.FromKey(obj4.Key, Main.player[num142].name));
			}
			break;
		}
		case 31:
		{
			if (Main.netMode != 2)
			{
				break;
			}
			int num216 = reader.ReadInt16();
			int num217 = reader.ReadInt16();
			int num218 = Chest.FindChest(num216, num217);
			if (num218 > -1 && Chest.UsingChest(num218) == -1)
			{
				for (int num219 = 0; num219 < 40; num219++)
				{
					NetMessage.TrySendData(32, whoAmI, -1, null, num218, num219);
				}
				NetMessage.TrySendData(33, whoAmI, -1, null, num218);
				Main.player[whoAmI].chest = num218;
				if (Main.myPlayer == whoAmI)
				{
					Main.recBigList = false;
				}
				NetMessage.TrySendData(80, -1, whoAmI, null, whoAmI, num218);
				if (Main.netMode == 2 && WorldGen.IsChestRigged(num216, num217))
				{
					Wiring.SetCurrentUser(whoAmI);
					Wiring.HitSwitch(num216, num217);
					Wiring.SetCurrentUser();
					NetMessage.TrySendData(59, -1, whoAmI, null, num216, num217);
				}
			}
			break;
		}
		case 32:
		{
			int num167 = reader.ReadInt16();
			int num168 = reader.ReadByte();
			int stack5 = reader.ReadInt16();
			int prefixWeWant3 = reader.ReadByte();
			int type8 = reader.ReadInt16();
			if (num167 >= 0 && num167 < 8000)
			{
				if (Main.chest[num167] == null)
				{
					Main.chest[num167] = new Chest();
				}
				if (Main.chest[num167].item[num168] == null)
				{
					Main.chest[num167].item[num168] = new Item();
				}
				Main.chest[num167].item[num168].netDefaults(type8);
				Main.chest[num167].item[num168].Prefix(prefixWeWant3);
				Main.chest[num167].item[num168].stack = stack5;
				Recipe.FindRecipes(canDelayCheck: true);
			}
			break;
		}
		case 33:
		{
			int num22 = reader.ReadInt16();
			int num23 = reader.ReadInt16();
			int num24 = reader.ReadInt16();
			int num25 = reader.ReadByte();
			string name = string.Empty;
			if (num25 != 0)
			{
				if (num25 <= 20)
				{
					name = reader.ReadString();
				}
				else if (num25 != 255)
				{
					num25 = 0;
				}
			}
			if (Main.netMode == 1)
			{
				Player player = Main.player[Main.myPlayer];
				if (player.chest == -1)
				{
					Main.playerInventory = true;
					SoundEngine.PlaySound(10);
				}
				else if (player.chest != num22 && num22 != -1)
				{
					Main.playerInventory = true;
					SoundEngine.PlaySound(12);
					Main.recBigList = false;
				}
				else if (player.chest != -1 && num22 == -1)
				{
					SoundEngine.PlaySound(11);
					Main.recBigList = false;
				}
				player.chest = num22;
				player.chestX = num23;
				player.chestY = num24;
				Recipe.FindRecipes(canDelayCheck: true);
				if (Main.tile[num23, num24].frameX >= 36 && Main.tile[num23, num24].frameX < 72)
				{
					AchievementsHelper.HandleSpecialEvent(Main.player[Main.myPlayer], 16);
				}
			}
			else
			{
				if (num25 != 0)
				{
					int chest = Main.player[whoAmI].chest;
					Chest chest2 = Main.chest[chest];
					chest2.name = name;
					NetMessage.TrySendData(69, -1, whoAmI, null, chest, chest2.x, chest2.y);
				}
				Main.player[whoAmI].chest = num22;
				Recipe.FindRecipes(canDelayCheck: true);
				NetMessage.TrySendData(80, -1, whoAmI, null, whoAmI, num22);
			}
			break;
		}
		case 34:
		{
			byte b11 = reader.ReadByte();
			int num202 = reader.ReadInt16();
			int num203 = reader.ReadInt16();
			int num204 = reader.ReadInt16();
			int num205 = reader.ReadInt16();
			if (Main.netMode == 2)
			{
				num205 = 0;
			}
			if (Main.netMode == 2)
			{
				switch (b11)
				{
				case 0:
				{
					int num208 = WorldGen.PlaceChest(num202, num203, 21, notNearOtherChests: false, num204);
					if (num208 == -1)
					{
						NetMessage.TrySendData(34, whoAmI, -1, null, b11, num202, num203, num204, num208);
						Item.NewItem(new EntitySource_TileBreak(num202, num203), num202 * 16, num203 * 16, 32, 32, Chest.chestItemSpawn[num204], 1, noBroadcast: true);
					}
					else
					{
						NetMessage.TrySendData(34, -1, -1, null, b11, num202, num203, num204, num208);
					}
					break;
				}
				case 1:
					if (Main.tile[num202, num203].type == 21)
					{
						Tile tile = Main.tile[num202, num203];
						if (tile.frameX % 36 != 0)
						{
							num202--;
						}
						if (tile.frameY % 36 != 0)
						{
							num203--;
						}
						int number = Chest.FindChest(num202, num203);
						WorldGen.KillTile(num202, num203);
						if (!tile.active())
						{
							NetMessage.TrySendData(34, -1, -1, null, b11, num202, num203, 0f, number);
						}
						break;
					}
					goto default;
				default:
					switch (b11)
					{
					case 2:
					{
						int num206 = WorldGen.PlaceChest(num202, num203, 88, notNearOtherChests: false, num204);
						if (num206 == -1)
						{
							NetMessage.TrySendData(34, whoAmI, -1, null, b11, num202, num203, num204, num206);
							Item.NewItem(new EntitySource_TileBreak(num202, num203), num202 * 16, num203 * 16, 32, 32, Chest.dresserItemSpawn[num204], 1, noBroadcast: true);
						}
						else
						{
							NetMessage.TrySendData(34, -1, -1, null, b11, num202, num203, num204, num206);
						}
						break;
					}
					case 3:
						if (Main.tile[num202, num203].type == 88)
						{
							Tile tile2 = Main.tile[num202, num203];
							num202 -= tile2.frameX % 54 / 18;
							if (tile2.frameY % 36 != 0)
							{
								num203--;
							}
							int number2 = Chest.FindChest(num202, num203);
							WorldGen.KillTile(num202, num203);
							if (!tile2.active())
							{
								NetMessage.TrySendData(34, -1, -1, null, b11, num202, num203, 0f, number2);
							}
							break;
						}
						goto default;
					default:
						switch (b11)
						{
						case 4:
						{
							int num207 = WorldGen.PlaceChest(num202, num203, 467, notNearOtherChests: false, num204);
							if (num207 == -1)
							{
								NetMessage.TrySendData(34, whoAmI, -1, null, b11, num202, num203, num204, num207);
								Item.NewItem(new EntitySource_TileBreak(num202, num203), num202 * 16, num203 * 16, 32, 32, Chest.chestItemSpawn2[num204], 1, noBroadcast: true);
							}
							else
							{
								NetMessage.TrySendData(34, -1, -1, null, b11, num202, num203, num204, num207);
							}
							break;
						}
						case 5:
							if (Main.tile[num202, num203].type == 467)
							{
								Tile tile3 = Main.tile[num202, num203];
								if (tile3.frameX % 36 != 0)
								{
									num202--;
								}
								if (tile3.frameY % 36 != 0)
								{
									num203--;
								}
								int number3 = Chest.FindChest(num202, num203);
								WorldGen.KillTile(num202, num203);
								if (!tile3.active())
								{
									NetMessage.TrySendData(34, -1, -1, null, b11, num202, num203, 0f, number3);
								}
							}
							break;
						}
						break;
					}
					break;
				}
				break;
			}
			switch (b11)
			{
			case 0:
				if (num205 == -1)
				{
					WorldGen.KillTile(num202, num203);
					break;
				}
				SoundEngine.PlaySound(0, num202 * 16, num203 * 16);
				WorldGen.PlaceChestDirect(num202, num203, 21, num204, num205);
				break;
			case 2:
				if (num205 == -1)
				{
					WorldGen.KillTile(num202, num203);
					break;
				}
				SoundEngine.PlaySound(0, num202 * 16, num203 * 16);
				WorldGen.PlaceDresserDirect(num202, num203, 88, num204, num205);
				break;
			case 4:
				if (num205 == -1)
				{
					WorldGen.KillTile(num202, num203);
					break;
				}
				SoundEngine.PlaySound(0, num202 * 16, num203 * 16);
				WorldGen.PlaceChestDirect(num202, num203, 467, num204, num205);
				break;
			default:
				Chest.DestroyChestDirect(num202, num203, num205);
				WorldGen.KillTile(num202, num203);
				break;
			}
			break;
		}
		case 35:
		{
			int num149 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num149 = whoAmI;
			}
			int num150 = reader.ReadInt16();
			if (num149 != Main.myPlayer || Main.ServerSideCharacter)
			{
				Main.player[num149].HealEffect(num150);
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(35, -1, whoAmI, null, num149, num150);
			}
			break;
		}
		case 36:
		{
			int num106 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num106 = whoAmI;
			}
			Player player7 = Main.player[num106];
			bool flag11 = player7.zone5[0];
			player7.zone1 = reader.ReadByte();
			player7.zone2 = reader.ReadByte();
			player7.zone3 = reader.ReadByte();
			player7.zone4 = reader.ReadByte();
			player7.zone5 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				if (!flag11 && player7.zone5[0])
				{
					NPC.SpawnFaelings(num106);
				}
				NetMessage.TrySendData(36, -1, whoAmI, null, num106);
			}
			break;
		}
		case 37:
			if (Main.netMode == 1)
			{
				if (Main.autoPass)
				{
					NetMessage.TrySendData(38);
					Main.autoPass = false;
				}
				else
				{
					Netplay.ServerPassword = "";
					Main.menuMode = 31;
				}
			}
			break;
		case 38:
			if (Main.netMode == 2)
			{
				if (reader.ReadString() == Netplay.ServerPassword)
				{
					Netplay.Clients[whoAmI].State = 1;
					NetMessage.TrySendData(3, whoAmI);
				}
				else
				{
					NetMessage.TrySendData(2, whoAmI, -1, Lang.mp[1].ToNetworkText());
				}
			}
			break;
		case 39:
			if (Main.netMode == 1)
			{
				int num44 = reader.ReadInt16();
				Main.item[num44].playerIndexTheItemIsReservedFor = 255;
				NetMessage.TrySendData(22, -1, -1, null, num44);
			}
			break;
		case 40:
		{
			int num252 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num252 = whoAmI;
			}
			int npcIndex = reader.ReadInt16();
			Main.player[num252].SetTalkNPC(npcIndex, fromNet: true);
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(40, -1, whoAmI, null, num252);
			}
			break;
		}
		case 41:
		{
			int num223 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num223 = whoAmI;
			}
			Player player13 = Main.player[num223];
			float itemRotation = reader.ReadSingle();
			int itemAnimation = reader.ReadInt16();
			player13.itemRotation = itemRotation;
			player13.itemAnimation = itemAnimation;
			player13.channel = player13.inventory[player13.selectedItem].channel;
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(41, -1, whoAmI, null, num223);
			}
			if (Main.netMode == 1)
			{
				Item item6 = player13.inventory[player13.selectedItem];
				if (item6.UseSound != null)
				{
					SoundEngine.PlaySound(item6.UseSound, player13.Center);
				}
			}
			break;
		}
		case 42:
		{
			int num201 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num201 = whoAmI;
			}
			else if (Main.myPlayer == num201 && !Main.ServerSideCharacter)
			{
				break;
			}
			int statMana = reader.ReadInt16();
			int statManaMax = reader.ReadInt16();
			Main.player[num201].statMana = statMana;
			Main.player[num201].statManaMax = statManaMax;
			break;
		}
		case 43:
		{
			int num154 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num154 = whoAmI;
			}
			int num155 = reader.ReadInt16();
			if (num154 != Main.myPlayer)
			{
				Main.player[num154].ManaEffect(num155);
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(43, -1, whoAmI, null, num154, num155);
			}
			break;
		}
		case 45:
		{
			int num122 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num122 = whoAmI;
			}
			int num123 = reader.ReadByte();
			Player player8 = Main.player[num122];
			int team = player8.team;
			player8.team = num123;
			Color color = Main.teamColor[num123];
			if (Main.netMode != 2)
			{
				break;
			}
			NetMessage.TrySendData(45, -1, whoAmI, null, num122);
			LocalizedText localizedText = Lang.mp[13 + num123];
			if (num123 == 5)
			{
				localizedText = Lang.mp[22];
			}
			for (int num124 = 0; num124 < 255; num124++)
			{
				if (num124 == whoAmI || (team > 0 && Main.player[num124].team == team) || (num123 > 0 && Main.player[num124].team == num123))
				{
					ChatHelper.SendChatMessageToClient(NetworkText.FromKey(localizedText.Key, player8.name), color, num124);
				}
			}
			break;
		}
		case 46:
			if (Main.netMode == 2)
			{
				short i3 = reader.ReadInt16();
				int j3 = reader.ReadInt16();
				int num116 = Sign.ReadSign(i3, j3);
				if (num116 >= 0)
				{
					NetMessage.TrySendData(47, whoAmI, -1, null, num116, whoAmI);
				}
			}
			break;
		case 47:
		{
			int num28 = reader.ReadInt16();
			int x2 = reader.ReadInt16();
			int y = reader.ReadInt16();
			string text4 = reader.ReadString();
			int num29 = reader.ReadByte();
			BitsByte bitsByte = reader.ReadByte();
			if (num28 >= 0 && num28 < 1000)
			{
				string text5 = null;
				if (Main.sign[num28] != null)
				{
					text5 = Main.sign[num28].text;
				}
				Main.sign[num28] = new Sign();
				Main.sign[num28].x = x2;
				Main.sign[num28].y = y;
				Sign.TextSign(num28, text4);
				if (Main.netMode == 2 && text5 != text4)
				{
					num29 = whoAmI;
					NetMessage.TrySendData(47, -1, whoAmI, null, num28, num29);
				}
				if (Main.netMode == 1 && num29 == Main.myPlayer && Main.sign[num28] != null && !bitsByte[0])
				{
					Main.playerInventory = false;
					Main.player[Main.myPlayer].SetTalkNPC(-1, fromNet: true);
					Main.npcChatCornerItem = 0;
					Main.editSign = false;
					SoundEngine.PlaySound(10);
					Main.player[Main.myPlayer].sign = num28;
					Main.npcChatText = Main.sign[num28].text;
				}
			}
			break;
		}
		case 48:
		{
			int num234 = reader.ReadInt16();
			int num235 = reader.ReadInt16();
			byte b14 = reader.ReadByte();
			byte liquidType = reader.ReadByte();
			if (Main.netMode == 2 && Netplay.SpamCheck)
			{
				int num236 = whoAmI;
				int num237 = (int)(Main.player[num236].position.X + (float)(Main.player[num236].width / 2));
				int num238 = (int)(Main.player[num236].position.Y + (float)(Main.player[num236].height / 2));
				int num239 = 10;
				int num240 = num237 - num239;
				int num241 = num237 + num239;
				int num242 = num238 - num239;
				int num243 = num238 + num239;
				if (num234 < num240 || num234 > num241 || num235 < num242 || num235 > num243)
				{
					Netplay.Clients[whoAmI].SpamWater += 1f;
				}
			}
			if (Main.tile[num234, num235] == null)
			{
				Main.tile[num234, num235] = new Tile();
			}
			lock (Main.tile[num234, num235])
			{
				Main.tile[num234, num235].liquid = b14;
				Main.tile[num234, num235].liquidType(liquidType);
				if (Main.netMode == 2)
				{
					WorldGen.SquareTileFrame(num234, num235);
					if (b14 == 0)
					{
						NetMessage.SendData(48, -1, whoAmI, null, num234, num235);
					}
				}
				break;
			}
		}
		case 49:
			if (Netplay.Connection.State == 6)
			{
				Netplay.Connection.State = 10;
				Main.player[Main.myPlayer].Spawn(PlayerSpawnContext.SpawningIntoWorld);
			}
			break;
		case 50:
		{
			int num183 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num183 = whoAmI;
			}
			else if (num183 == Main.myPlayer && !Main.ServerSideCharacter)
			{
				break;
			}
			Player player12 = Main.player[num183];
			for (int num184 = 0; num184 < Player.maxBuffs; num184++)
			{
				player12.buffType[num184] = reader.ReadUInt16();
				if (player12.buffType[num184] > 0)
				{
					player12.buffTime[num184] = 60;
				}
				else
				{
					player12.buffTime[num184] = 0;
				}
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(50, -1, whoAmI, null, num183);
			}
			break;
		}
		case 51:
		{
			byte b7 = reader.ReadByte();
			byte b8 = reader.ReadByte();
			switch (b8)
			{
			case 1:
				NPC.SpawnSkeletron(b7);
				break;
			case 2:
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(51, -1, whoAmI, null, b7, (int)b8);
				}
				else
				{
					SoundEngine.PlaySound(SoundID.Item1, (int)Main.player[b7].position.X, (int)Main.player[b7].position.Y);
				}
				break;
			case 3:
				if (Main.netMode == 2)
				{
					Main.Sundialing();
				}
				break;
			case 4:
				Main.npc[b7].BigMimicSpawnSmoke();
				break;
			case 5:
				if (Main.netMode == 2)
				{
					NPC nPC6 = new NPC();
					nPC6.SetDefaults(664);
					Main.BestiaryTracker.Kills.RegisterKill(nPC6);
				}
				break;
			case 6:
				if (Main.netMode == 2)
				{
					Main.Moondialing();
				}
				break;
			}
			break;
		}
		case 52:
		{
			int num151 = reader.ReadByte();
			int num152 = reader.ReadInt16();
			int num153 = reader.ReadInt16();
			if (num151 == 1)
			{
				Chest.Unlock(num152, num153);
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(52, -1, whoAmI, null, 0, num151, num152, num153);
					NetMessage.SendTileSquare(-1, num152, num153, 2);
				}
			}
			if (num151 == 2)
			{
				WorldGen.UnlockDoor(num152, num153);
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(52, -1, whoAmI, null, 0, num151, num152, num153);
					NetMessage.SendTileSquare(-1, num152, num153, 2);
				}
			}
			if (num151 == 3)
			{
				Chest.Lock(num152, num153);
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(52, -1, whoAmI, null, 0, num151, num152, num153);
					NetMessage.SendTileSquare(-1, num152, num153, 2);
				}
			}
			break;
		}
		case 53:
		{
			int num127 = reader.ReadInt16();
			int type6 = reader.ReadUInt16();
			int time2 = reader.ReadInt16();
			Main.npc[num127].AddBuff(type6, time2, quiet: true);
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(54, -1, -1, null, num127);
			}
			break;
		}
		case 54:
			if (Main.netMode == 1)
			{
				int num114 = reader.ReadInt16();
				NPC nPC4 = Main.npc[num114];
				for (int num115 = 0; num115 < NPC.maxBuffs; num115++)
				{
					nPC4.buffType[num115] = reader.ReadUInt16();
					nPC4.buffTime[num115] = reader.ReadInt16();
				}
			}
			break;
		case 55:
		{
			int num72 = reader.ReadByte();
			int num73 = reader.ReadUInt16();
			int num74 = reader.ReadInt32();
			if (Main.netMode != 2 || num72 == whoAmI || Main.pvpBuff[num73])
			{
				if (Main.netMode == 1 && num72 == Main.myPlayer)
				{
					Main.player[num72].AddBuff(num73, num74);
				}
				else if (Main.netMode == 2)
				{
					NetMessage.TrySendData(55, -1, -1, null, num72, num73, num74);
				}
			}
			break;
		}
		case 56:
		{
			int num48 = reader.ReadInt16();
			if (num48 >= 0 && num48 < 200)
			{
				if (Main.netMode == 1)
				{
					string givenName = reader.ReadString();
					Main.npc[num48].GivenName = givenName;
					int townNpcVariationIndex = reader.ReadInt32();
					Main.npc[num48].townNpcVariationIndex = townNpcVariationIndex;
				}
				else if (Main.netMode == 2)
				{
					NetMessage.TrySendData(56, whoAmI, -1, null, num48);
				}
			}
			break;
		}
		case 57:
			if (Main.netMode == 1)
			{
				WorldGen.tGood = reader.ReadByte();
				WorldGen.tEvil = reader.ReadByte();
				WorldGen.tBlood = reader.ReadByte();
			}
			break;
		case 58:
		{
			int num253 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num253 = whoAmI;
			}
			float num254 = reader.ReadSingle();
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(58, -1, whoAmI, null, whoAmI, num254);
				break;
			}
			Player player14 = Main.player[num253];
			int type14 = player14.inventory[player14.selectedItem].type;
			switch (type14)
			{
			case 4057:
			case 4372:
			case 4715:
				player14.PlayGuitarChord(num254);
				break;
			case 4673:
				player14.PlayDrums(num254);
				break;
			default:
			{
				Main.musicPitch = num254;
				LegacySoundStyle type15 = SoundID.Item26;
				if (type14 == 507)
				{
					type15 = SoundID.Item35;
				}
				if (type14 == 1305)
				{
					type15 = SoundID.Item47;
				}
				SoundEngine.PlaySound(type15, player14.position);
				break;
			}
			}
			break;
		}
		case 59:
		{
			int num30 = reader.ReadInt16();
			int num31 = reader.ReadInt16();
			Wiring.SetCurrentUser(whoAmI);
			Wiring.HitSwitch(num30, num31);
			Wiring.SetCurrentUser();
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(59, -1, whoAmI, null, num30, num31);
			}
			break;
		}
		case 60:
		{
			int num247 = reader.ReadInt16();
			int num248 = reader.ReadInt16();
			int num249 = reader.ReadInt16();
			byte b15 = reader.ReadByte();
			if (num247 >= 200)
			{
				NetMessage.BootPlayer(whoAmI, NetworkText.FromKey("Net.CheatingInvalid"));
				break;
			}
			NPC nPC7 = Main.npc[num247];
			bool isLikeATownNPC = nPC7.isLikeATownNPC;
			if (Main.netMode == 1)
			{
				nPC7.homeless = b15 == 1;
				nPC7.homeTileX = num248;
				nPC7.homeTileY = num249;
			}
			if (!isLikeATownNPC)
			{
				break;
			}
			if (Main.netMode == 1)
			{
				switch (b15)
				{
				case 1:
					WorldGen.TownManager.KickOut(nPC7.type);
					break;
				case 2:
					WorldGen.TownManager.SetRoom(nPC7.type, num248, num249);
					break;
				}
			}
			else if (b15 == 1)
			{
				WorldGen.kickOut(num247);
			}
			else
			{
				WorldGen.moveRoom(num248, num249, num247);
			}
			break;
		}
		case 61:
		{
			int num212 = reader.ReadInt16();
			int num213 = reader.ReadInt16();
			if (Main.netMode != 2)
			{
				break;
			}
			if (num213 >= 0 && num213 < NPCID.Count && NPCID.Sets.MPAllowedEnemies[num213])
			{
				if (!NPC.AnyNPCs(num213))
				{
					NPC.SpawnOnPlayer(num212, num213);
				}
			}
			else if (num213 == -4)
			{
				if (!Main.dayTime && !DD2Event.Ongoing)
				{
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.misc[31].Key), new Color(50, 255, 130));
					Main.startPumpkinMoon();
					NetMessage.TrySendData(7);
					NetMessage.TrySendData(78, -1, -1, null, 0, 1f, 2f, 1f);
				}
			}
			else if (num213 == -5)
			{
				if (!Main.dayTime && !DD2Event.Ongoing)
				{
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.misc[34].Key), new Color(50, 255, 130));
					Main.startSnowMoon();
					NetMessage.TrySendData(7);
					NetMessage.TrySendData(78, -1, -1, null, 0, 1f, 1f, 1f);
				}
			}
			else if (num213 == -6)
			{
				if (Main.dayTime && !Main.eclipse)
				{
					if (Main.remixWorld)
					{
						ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.misc[106].Key), new Color(50, 255, 130));
					}
					else
					{
						ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.misc[20].Key), new Color(50, 255, 130));
					}
					Main.eclipse = true;
					NetMessage.TrySendData(7);
				}
			}
			else if (num213 == -7)
			{
				Main.invasionDelay = 0;
				Main.StartInvasion(4);
				NetMessage.TrySendData(7);
				NetMessage.TrySendData(78, -1, -1, null, 0, 1f, Main.invasionType + 3);
			}
			else if (num213 == -8)
			{
				if (NPC.downedGolemBoss && Main.hardMode && !NPC.AnyDanger() && !NPC.AnyoneNearCultists())
				{
					WorldGen.StartImpendingDoom(720);
					NetMessage.TrySendData(7);
				}
			}
			else if (num213 == -10)
			{
				if (!Main.dayTime && !Main.bloodMoon)
				{
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.misc[8].Key), new Color(50, 255, 130));
					Main.bloodMoon = true;
					if (Main.GetMoonPhase() == MoonPhase.Empty)
					{
						Main.moonPhase = 5;
					}
					AchievementsHelper.NotifyProgressionEvent(4);
					NetMessage.TrySendData(7);
				}
			}
			else if (num213 == -11)
			{
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Misc.CombatBookUsed"), new Color(50, 255, 130));
				NPC.combatBookWasUsed = true;
				NetMessage.TrySendData(7);
			}
			else if (num213 == -12)
			{
				NPC.UnlockOrExchangePet(ref NPC.boughtCat, 637, "Misc.LicenseCatUsed", num213);
			}
			else if (num213 == -13)
			{
				NPC.UnlockOrExchangePet(ref NPC.boughtDog, 638, "Misc.LicenseDogUsed", num213);
			}
			else if (num213 == -14)
			{
				NPC.UnlockOrExchangePet(ref NPC.boughtBunny, 656, "Misc.LicenseBunnyUsed", num213);
			}
			else if (num213 == -15)
			{
				NPC.UnlockOrExchangePet(ref NPC.unlockedSlimeBlueSpawn, 670, "Misc.LicenseSlimeUsed", num213);
			}
			else if (num213 == -16)
			{
				NPC.SpawnMechQueen(num212);
			}
			else if (num213 == -17)
			{
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Misc.CombatBookVolumeTwoUsed"), new Color(50, 255, 130));
				NPC.combatBookVolumeTwoWasUsed = true;
				NetMessage.TrySendData(7);
			}
			else if (num213 == -18)
			{
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Misc.PeddlersSatchelUsed"), new Color(50, 255, 130));
				NPC.peddlersSatchelWasUsed = true;
				NetMessage.TrySendData(7);
			}
			else if (num213 < 0)
			{
				int num214 = 1;
				if (num213 > -InvasionID.Count)
				{
					num214 = -num213;
				}
				if (num214 > 0 && Main.invasionType == 0)
				{
					Main.invasionDelay = 0;
					Main.StartInvasion(num214);
				}
				NetMessage.TrySendData(78, -1, -1, null, 0, 1f, Main.invasionType + 3);
			}
			break;
		}
		case 62:
		{
			int num169 = reader.ReadByte();
			int num170 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num169 = whoAmI;
			}
			if (num170 == 1)
			{
				Main.player[num169].NinjaDodge();
			}
			if (num170 == 2)
			{
				Main.player[num169].ShadowDodge();
			}
			if (num170 == 4)
			{
				Main.player[num169].BrainOfConfusionDodge();
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(62, -1, whoAmI, null, num169, num170);
			}
			break;
		}
		case 63:
		{
			int num147 = reader.ReadInt16();
			int num148 = reader.ReadInt16();
			byte b4 = reader.ReadByte();
			byte b5 = reader.ReadByte();
			if (b5 == 0)
			{
				WorldGen.paintTile(num147, num148, b4);
			}
			else
			{
				WorldGen.paintCoatTile(num147, num148, b4);
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(63, -1, whoAmI, null, num147, num148, (int)b4, (int)b5);
			}
			break;
		}
		case 64:
		{
			int num136 = reader.ReadInt16();
			int num137 = reader.ReadInt16();
			byte b2 = reader.ReadByte();
			byte b3 = reader.ReadByte();
			if (b3 == 0)
			{
				WorldGen.paintWall(num136, num137, b2);
			}
			else
			{
				WorldGen.paintCoatWall(num136, num137, b2);
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(64, -1, whoAmI, null, num136, num137, (int)b2, (int)b3);
			}
			break;
		}
		case 65:
		{
			BitsByte bitsByte6 = reader.ReadByte();
			int num75 = reader.ReadInt16();
			if (Main.netMode == 2)
			{
				num75 = whoAmI;
			}
			Vector2 vector5 = reader.ReadVector2();
			int num76 = 0;
			num76 = reader.ReadByte();
			int num77 = 0;
			if (bitsByte6[0])
			{
				num77++;
			}
			if (bitsByte6[1])
			{
				num77 += 2;
			}
			bool flag9 = false;
			if (bitsByte6[2])
			{
				flag9 = true;
			}
			int num78 = 0;
			if (bitsByte6[3])
			{
				num78 = reader.ReadInt32();
			}
			if (flag9)
			{
				vector5 = Main.player[num75].position;
			}
			switch (num77)
			{
			case 0:
				Main.player[num75].Teleport(vector5, num76, num78);
				break;
			case 1:
				Main.npc[num75].Teleport(vector5, num76, num78);
				break;
			case 2:
			{
				Main.player[num75].Teleport(vector5, num76, num78);
				if (Main.netMode != 2)
				{
					break;
				}
				RemoteClient.CheckSection(whoAmI, vector5);
				NetMessage.TrySendData(65, -1, -1, null, 0, num75, vector5.X, vector5.Y, num76, flag9.ToInt(), num78);
				int num79 = -1;
				float num80 = 9999f;
				for (int num81 = 0; num81 < 255; num81++)
				{
					if (Main.player[num81].active && num81 != whoAmI)
					{
						Vector2 vector6 = Main.player[num81].position - Main.player[whoAmI].position;
						if (vector6.Length() < num80)
						{
							num80 = vector6.Length();
							num79 = num81;
						}
					}
				}
				if (num79 >= 0)
				{
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Game.HasTeleportedTo", Main.player[whoAmI].name, Main.player[num79].name), new Color(250, 250, 0));
				}
				break;
			}
			}
			if (Main.netMode == 2 && num77 == 0)
			{
				NetMessage.TrySendData(65, -1, whoAmI, null, num77, num75, vector5.X, vector5.Y, num76, flag9.ToInt(), num78);
			}
			break;
		}
		case 66:
		{
			int num51 = reader.ReadByte();
			int num52 = reader.ReadInt16();
			if (num52 > 0)
			{
				Player player3 = Main.player[num51];
				player3.statLife += num52;
				if (player3.statLife > player3.statLifeMax2)
				{
					player3.statLife = player3.statLifeMax2;
				}
				player3.HealEffect(num52, broadcast: false);
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(66, -1, whoAmI, null, num51, num52);
				}
			}
			break;
		}
		case 68:
			reader.ReadString();
			break;
		case 69:
		{
			int num244 = reader.ReadInt16();
			int num245 = reader.ReadInt16();
			int num246 = reader.ReadInt16();
			if (Main.netMode == 1)
			{
				if (num244 >= 0 && num244 < 8000)
				{
					Chest chest3 = Main.chest[num244];
					if (chest3 == null)
					{
						chest3 = new Chest();
						chest3.x = num245;
						chest3.y = num246;
						Main.chest[num244] = chest3;
					}
					else if (chest3.x != num245 || chest3.y != num246)
					{
						break;
					}
					chest3.name = reader.ReadString();
				}
			}
			else
			{
				if (num244 < -1 || num244 >= 8000)
				{
					break;
				}
				if (num244 == -1)
				{
					num244 = Chest.FindChest(num245, num246);
					if (num244 == -1)
					{
						break;
					}
				}
				Chest chest4 = Main.chest[num244];
				if (chest4.x == num245 && chest4.y == num246)
				{
					NetMessage.TrySendData(69, whoAmI, -1, null, num244, num245, num246);
				}
			}
			break;
		}
		case 70:
			if (Main.netMode == 2)
			{
				int num224 = reader.ReadInt16();
				int who = reader.ReadByte();
				if (Main.netMode == 2)
				{
					who = whoAmI;
				}
				if (num224 < 200 && num224 >= 0)
				{
					NPC.CatchNPC(num224, who);
				}
			}
			break;
		case 71:
			if (Main.netMode == 2)
			{
				int x14 = reader.ReadInt32();
				int y13 = reader.ReadInt32();
				int type11 = reader.ReadInt16();
				byte style3 = reader.ReadByte();
				NPC.ReleaseNPC(x14, y13, type11, style3, whoAmI);
			}
			break;
		case 72:
			if (Main.netMode == 1)
			{
				for (int num209 = 0; num209 < 40; num209++)
				{
					Main.travelShop[num209] = reader.ReadInt16();
				}
			}
			break;
		case 73:
			switch (reader.ReadByte())
			{
			case 0:
				Main.player[whoAmI].TeleportationPotion();
				break;
			case 1:
				Main.player[whoAmI].MagicConch();
				break;
			case 2:
				Main.player[whoAmI].DemonConch();
				break;
			case 3:
				Main.player[whoAmI].Shellphone_Spawn();
				break;
			}
			break;
		case 74:
			if (Main.netMode == 1)
			{
				Main.anglerQuest = reader.ReadByte();
				Main.anglerQuestFinished = reader.ReadBoolean();
			}
			break;
		case 75:
			if (Main.netMode == 2)
			{
				string name2 = Main.player[whoAmI].name;
				if (!Main.anglerWhoFinishedToday.Contains(name2))
				{
					Main.anglerWhoFinishedToday.Add(name2);
				}
			}
			break;
		case 76:
		{
			int num174 = reader.ReadByte();
			if (num174 != Main.myPlayer || Main.ServerSideCharacter)
			{
				if (Main.netMode == 2)
				{
					num174 = whoAmI;
				}
				Player obj6 = Main.player[num174];
				obj6.anglerQuestsFinished = reader.ReadInt32();
				obj6.golferScoreAccumulated = reader.ReadInt32();
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(76, -1, whoAmI, null, num174);
				}
			}
			break;
		}
		case 77:
		{
			short type9 = reader.ReadInt16();
			ushort tileType = reader.ReadUInt16();
			short x12 = reader.ReadInt16();
			short y11 = reader.ReadInt16();
			Animation.NewTemporaryAnimation(type9, tileType, x12, y11);
			break;
		}
		case 78:
			if (Main.netMode == 1)
			{
				Main.ReportInvasionProgress(reader.ReadInt32(), reader.ReadInt32(), reader.ReadSByte(), reader.ReadSByte());
			}
			break;
		case 79:
		{
			int x10 = reader.ReadInt16();
			int y9 = reader.ReadInt16();
			short type7 = reader.ReadInt16();
			int style2 = reader.ReadInt16();
			int num145 = reader.ReadByte();
			int random = reader.ReadSByte();
			int direction = (reader.ReadBoolean() ? 1 : (-1));
			if (Main.netMode == 2)
			{
				Netplay.Clients[whoAmI].SpamAddBlock += 1f;
				if (!WorldGen.InWorld(x10, y9, 10) || !Netplay.Clients[whoAmI].TileSections[Netplay.GetSectionX(x10), Netplay.GetSectionY(y9)])
				{
					break;
				}
			}
			WorldGen.PlaceObject(x10, y9, type7, mute: false, style2, num145, random, direction);
			if (Main.netMode == 2)
			{
				NetMessage.SendObjectPlacement(whoAmI, x10, y9, type7, style2, num145, random, direction);
			}
			break;
		}
		case 80:
			if (Main.netMode == 1)
			{
				int num132 = reader.ReadByte();
				int num133 = reader.ReadInt16();
				if (num133 >= -3 && num133 < 8000)
				{
					Main.player[num132].chest = num133;
					Recipe.FindRecipes(canDelayCheck: true);
				}
			}
			break;
		case 81:
			if (Main.netMode == 1)
			{
				int x8 = (int)reader.ReadSingle();
				int y7 = (int)reader.ReadSingle();
				CombatText.NewText(color: reader.ReadRGB(), amount: reader.ReadInt32(), location: new Rectangle(x8, y7, 0, 0));
			}
			break;
		case 119:
			if (Main.netMode == 1)
			{
				int x9 = (int)reader.ReadSingle();
				int y8 = (int)reader.ReadSingle();
				CombatText.NewText(color: reader.ReadRGB(), text: NetworkText.Deserialize(reader).ToString(), location: new Rectangle(x9, y8, 0, 0));
			}
			break;
		case 82:
			NetManager.Instance.Read(reader, whoAmI, length);
			break;
		case 83:
			if (Main.netMode == 1)
			{
				int num108 = reader.ReadInt16();
				int num109 = reader.ReadInt32();
				if (num108 >= 0 && num108 < 290)
				{
					NPC.killCount[num108] = num109;
				}
			}
			break;
		case 84:
		{
			int num107 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num107 = whoAmI;
			}
			float stealth = reader.ReadSingle();
			Main.player[num107].stealth = stealth;
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(84, -1, whoAmI, null, num107);
			}
			break;
		}
		case 85:
		{
			int num102 = whoAmI;
			int slot = reader.ReadInt16();
			if (Main.netMode == 2 && num102 < 255)
			{
				Chest.ServerPlaceItem(whoAmI, slot);
			}
			break;
		}
		case 86:
		{
			if (Main.netMode != 1)
			{
				break;
			}
			int num91 = reader.ReadInt32();
			if (!reader.ReadBoolean())
			{
				if (TileEntity.ByID.TryGetValue(num91, out var value9))
				{
					TileEntity.ByID.Remove(num91);
					TileEntity.ByPosition.Remove(value9.Position);
				}
			}
			else
			{
				TileEntity tileEntity = TileEntity.Read(reader, networkSend: true);
				tileEntity.ID = num91;
				TileEntity.ByID[tileEntity.ID] = tileEntity;
				TileEntity.ByPosition[tileEntity.Position] = tileEntity;
			}
			break;
		}
		case 87:
			if (Main.netMode == 2)
			{
				int x7 = reader.ReadInt16();
				int y6 = reader.ReadInt16();
				int type3 = reader.ReadByte();
				if (WorldGen.InWorld(x7, y6) && !TileEntity.ByPosition.ContainsKey(new Point16(x7, y6)))
				{
					TileEntity.PlaceEntityNet(x7, y6, type3);
				}
			}
			break;
		case 88:
		{
			if (Main.netMode != 1)
			{
				break;
			}
			int num215 = reader.ReadInt16();
			if (num215 < 0 || num215 > 400)
			{
				break;
			}
			Item item5 = Main.item[num215];
			BitsByte bitsByte14 = reader.ReadByte();
			if (bitsByte14[0])
			{
				item5.color.PackedValue = reader.ReadUInt32();
			}
			if (bitsByte14[1])
			{
				item5.damage = reader.ReadUInt16();
			}
			if (bitsByte14[2])
			{
				item5.knockBack = reader.ReadSingle();
			}
			if (bitsByte14[3])
			{
				item5.useAnimation = reader.ReadUInt16();
			}
			if (bitsByte14[4])
			{
				item5.useTime = reader.ReadUInt16();
			}
			if (bitsByte14[5])
			{
				item5.shoot = reader.ReadInt16();
			}
			if (bitsByte14[6])
			{
				item5.shootSpeed = reader.ReadSingle();
			}
			if (bitsByte14[7])
			{
				bitsByte14 = reader.ReadByte();
				if (bitsByte14[0])
				{
					item5.width = reader.ReadInt16();
				}
				if (bitsByte14[1])
				{
					item5.height = reader.ReadInt16();
				}
				if (bitsByte14[2])
				{
					item5.scale = reader.ReadSingle();
				}
				if (bitsByte14[3])
				{
					item5.ammo = reader.ReadInt16();
				}
				if (bitsByte14[4])
				{
					item5.useAmmo = reader.ReadInt16();
				}
				if (bitsByte14[5])
				{
					item5.notAmmo = reader.ReadBoolean();
				}
			}
			break;
		}
		case 89:
			if (Main.netMode == 2)
			{
				short x13 = reader.ReadInt16();
				int y12 = reader.ReadInt16();
				int netid3 = reader.ReadInt16();
				int prefix3 = reader.ReadByte();
				int stack6 = reader.ReadInt16();
				TEItemFrame.TryPlacing(x13, y12, netid3, prefix3, stack6);
			}
			break;
		case 91:
		{
			if (Main.netMode != 1)
			{
				break;
			}
			int num188 = reader.ReadInt32();
			int num189 = reader.ReadByte();
			if (num189 == 255)
			{
				if (EmoteBubble.byID.ContainsKey(num188))
				{
					EmoteBubble.byID.Remove(num188);
				}
				break;
			}
			int num190 = reader.ReadUInt16();
			int num191 = reader.ReadUInt16();
			int num192 = reader.ReadByte();
			int metadata = 0;
			if (num192 < 0)
			{
				metadata = reader.ReadInt16();
			}
			WorldUIAnchor worldUIAnchor = EmoteBubble.DeserializeNetAnchor(num189, num190);
			if (num189 == 1)
			{
				Main.player[num190].emoteTime = 360;
			}
			lock (EmoteBubble.byID)
			{
				if (!EmoteBubble.byID.ContainsKey(num188))
				{
					EmoteBubble.byID[num188] = new EmoteBubble(num192, worldUIAnchor, num191);
				}
				else
				{
					EmoteBubble.byID[num188].lifeTime = num191;
					EmoteBubble.byID[num188].lifeTimeStart = num191;
					EmoteBubble.byID[num188].emote = num192;
					EmoteBubble.byID[num188].anchor = worldUIAnchor;
				}
				EmoteBubble.byID[num188].ID = num188;
				EmoteBubble.byID[num188].metadata = metadata;
				EmoteBubble.OnBubbleChange(num188);
				break;
			}
		}
		case 92:
		{
			int num175 = reader.ReadInt16();
			int num176 = reader.ReadInt32();
			float num177 = reader.ReadSingle();
			float num178 = reader.ReadSingle();
			if (num175 >= 0 && num175 <= 200)
			{
				if (Main.netMode == 1)
				{
					Main.npc[num175].moneyPing(new Vector2(num177, num178));
					Main.npc[num175].extraValue = num176;
				}
				else
				{
					Main.npc[num175].extraValue += num176;
					NetMessage.TrySendData(92, -1, -1, null, num175, Main.npc[num175].extraValue, num177, num178);
				}
			}
			break;
		}
		case 95:
		{
			ushort num171 = reader.ReadUInt16();
			int num172 = reader.ReadByte();
			if (Main.netMode != 2)
			{
				break;
			}
			for (int num173 = 0; num173 < 1000; num173++)
			{
				if (Main.projectile[num173].owner == num171 && Main.projectile[num173].active && Main.projectile[num173].type == 602 && Main.projectile[num173].ai[1] == (float)num172)
				{
					Main.projectile[num173].Kill();
					NetMessage.TrySendData(29, -1, -1, null, Main.projectile[num173].identity, (int)num171);
					break;
				}
			}
			break;
		}
		case 96:
		{
			int num165 = reader.ReadByte();
			Player obj5 = Main.player[num165];
			int num166 = reader.ReadInt16();
			Vector2 newPos2 = reader.ReadVector2();
			Vector2 velocity6 = reader.ReadVector2();
			int lastPortalColorIndex2 = num166 + ((num166 % 2 == 0) ? 1 : (-1));
			obj5.lastPortalColorIndex = lastPortalColorIndex2;
			obj5.Teleport(newPos2, 4, num166);
			obj5.velocity = velocity6;
			if (Main.netMode == 2)
			{
				NetMessage.SendData(96, -1, -1, null, num165, newPos2.X, newPos2.Y, num166);
			}
			break;
		}
		case 97:
			if (Main.netMode == 1)
			{
				AchievementsHelper.NotifyNPCKilledDirect(Main.player[Main.myPlayer], reader.ReadInt16());
			}
			break;
		case 98:
			if (Main.netMode == 1)
			{
				AchievementsHelper.NotifyProgressionEvent(reader.ReadInt16());
			}
			break;
		case 99:
		{
			int num146 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num146 = whoAmI;
			}
			Main.player[num146].MinionRestTargetPoint = reader.ReadVector2();
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(99, -1, whoAmI, null, num146);
			}
			break;
		}
		case 115:
		{
			int num141 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num141 = whoAmI;
			}
			Main.player[num141].MinionAttackTargetNPC = reader.ReadInt16();
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(115, -1, whoAmI, null, num141);
			}
			break;
		}
		case 100:
		{
			int num134 = reader.ReadUInt16();
			NPC obj3 = Main.npc[num134];
			int num135 = reader.ReadInt16();
			Vector2 newPos = reader.ReadVector2();
			Vector2 velocity4 = reader.ReadVector2();
			int lastPortalColorIndex = num135 + ((num135 % 2 == 0) ? 1 : (-1));
			obj3.lastPortalColorIndex = lastPortalColorIndex;
			obj3.Teleport(newPos, 4, num135);
			obj3.velocity = velocity4;
			obj3.netOffset *= 0f;
			break;
		}
		case 101:
			if (Main.netMode != 2)
			{
				NPC.ShieldStrengthTowerSolar = reader.ReadUInt16();
				NPC.ShieldStrengthTowerVortex = reader.ReadUInt16();
				NPC.ShieldStrengthTowerNebula = reader.ReadUInt16();
				NPC.ShieldStrengthTowerStardust = reader.ReadUInt16();
				if (NPC.ShieldStrengthTowerSolar < 0)
				{
					NPC.ShieldStrengthTowerSolar = 0;
				}
				if (NPC.ShieldStrengthTowerVortex < 0)
				{
					NPC.ShieldStrengthTowerVortex = 0;
				}
				if (NPC.ShieldStrengthTowerNebula < 0)
				{
					NPC.ShieldStrengthTowerNebula = 0;
				}
				if (NPC.ShieldStrengthTowerStardust < 0)
				{
					NPC.ShieldStrengthTowerStardust = 0;
				}
				if (NPC.ShieldStrengthTowerSolar > NPC.LunarShieldPowerMax)
				{
					NPC.ShieldStrengthTowerSolar = NPC.LunarShieldPowerMax;
				}
				if (NPC.ShieldStrengthTowerVortex > NPC.LunarShieldPowerMax)
				{
					NPC.ShieldStrengthTowerVortex = NPC.LunarShieldPowerMax;
				}
				if (NPC.ShieldStrengthTowerNebula > NPC.LunarShieldPowerMax)
				{
					NPC.ShieldStrengthTowerNebula = NPC.LunarShieldPowerMax;
				}
				if (NPC.ShieldStrengthTowerStardust > NPC.LunarShieldPowerMax)
				{
					NPC.ShieldStrengthTowerStardust = NPC.LunarShieldPowerMax;
				}
			}
			break;
		case 102:
		{
			int num82 = reader.ReadByte();
			ushort num83 = reader.ReadUInt16();
			Vector2 other = reader.ReadVector2();
			if (Main.netMode == 2)
			{
				num82 = whoAmI;
				NetMessage.TrySendData(102, -1, -1, null, num82, (int)num83, other.X, other.Y);
				break;
			}
			Player player4 = Main.player[num82];
			for (int num84 = 0; num84 < 255; num84++)
			{
				Player player5 = Main.player[num84];
				if (!player5.active || player5.dead || (player4.team != 0 && player4.team != player5.team) || !(player5.Distance(other) < 700f))
				{
					continue;
				}
				Vector2 value8 = player4.Center - player5.Center;
				Vector2 vector7 = Vector2.Normalize(value8);
				if (!vector7.HasNaNs())
				{
					int type4 = 90;
					float num85 = 0f;
					float num86 = (float)Math.PI / 15f;
					Vector2 spinningpoint = new Vector2(0f, -8f);
					Vector2 vector8 = new Vector2(-3f);
					float num87 = 0f;
					float num88 = 0.005f;
					switch (num83)
					{
					case 179:
						type4 = 86;
						break;
					case 173:
						type4 = 90;
						break;
					case 176:
						type4 = 88;
						break;
					}
					for (int num89 = 0; (float)num89 < value8.Length() / 6f; num89++)
					{
						Vector2 position2 = player5.Center + 6f * (float)num89 * vector7 + spinningpoint.RotatedBy(num85) + vector8;
						num85 += num86;
						int num90 = Dust.NewDust(position2, 6, 6, type4, 0f, 0f, 100, default(Color), 1.5f);
						Main.dust[num90].noGravity = true;
						Main.dust[num90].velocity = Vector2.Zero;
						num87 = (Main.dust[num90].fadeIn = num87 + num88);
						Main.dust[num90].velocity += vector7 * 1.5f;
					}
				}
				player5.NebulaLevelup(num83);
			}
			break;
		}
		case 103:
			if (Main.netMode == 1)
			{
				NPC.MaxMoonLordCountdown = reader.ReadInt32();
				NPC.MoonLordCountdown = reader.ReadInt32();
			}
			break;
		case 104:
			if (Main.netMode == 1 && Main.npcShop > 0)
			{
				Item[] item = Main.instance.shop[Main.npcShop].item;
				int num71 = reader.ReadByte();
				int type2 = reader.ReadInt16();
				int stack2 = reader.ReadInt16();
				int prefixWeWant = reader.ReadByte();
				int value7 = reader.ReadInt32();
				BitsByte bitsByte5 = reader.ReadByte();
				if (num71 < item.Length)
				{
					item[num71] = new Item();
					item[num71].netDefaults(type2);
					item[num71].stack = stack2;
					item[num71].Prefix(prefixWeWant);
					item[num71].value = value7;
					item[num71].buyOnce = bitsByte5[0];
				}
			}
			break;
		case 105:
			if (Main.netMode != 1)
			{
				short i2 = reader.ReadInt16();
				int j2 = reader.ReadInt16();
				bool on = reader.ReadBoolean();
				WorldGen.ToggleGemLock(i2, j2, on);
			}
			break;
		case 106:
			if (Main.netMode == 1)
			{
				HalfVector2 halfVector = default(HalfVector2);
				halfVector.PackedValue = reader.ReadUInt32();
				Utils.PoofOfSmoke(halfVector.ToVector2());
			}
			break;
		case 107:
			if (Main.netMode == 1)
			{
				Color c = reader.ReadRGB();
				string text6 = NetworkText.Deserialize(reader).ToString();
				int widthLimit = reader.ReadInt16();
				Main.NewTextMultiline(text6, force: false, c, widthLimit);
			}
			break;
		case 108:
			if (Main.netMode == 1)
			{
				int damage = reader.ReadInt16();
				float knockBack = reader.ReadSingle();
				int x5 = reader.ReadInt16();
				int y4 = reader.ReadInt16();
				int angle = reader.ReadInt16();
				int ammo = reader.ReadInt16();
				int num53 = reader.ReadByte();
				if (num53 == Main.myPlayer)
				{
					WorldGen.ShootFromCannon(x5, y4, angle, ammo, damage, knockBack, num53, fromWire: true);
				}
			}
			break;
		case 109:
			if (Main.netMode == 2)
			{
				short x3 = reader.ReadInt16();
				int y2 = reader.ReadInt16();
				int x4 = reader.ReadInt16();
				int y3 = reader.ReadInt16();
				byte toolMode = reader.ReadByte();
				int num50 = whoAmI;
				WiresUI.Settings.MultiToolMode toolMode2 = WiresUI.Settings.ToolMode;
				WiresUI.Settings.ToolMode = (WiresUI.Settings.MultiToolMode)toolMode;
				Wiring.MassWireOperation(new Point(x3, y2), new Point(x4, y3), Main.player[num50]);
				WiresUI.Settings.ToolMode = toolMode2;
			}
			break;
		case 110:
		{
			if (Main.netMode != 1)
			{
				break;
			}
			int type = reader.ReadInt16();
			int num39 = reader.ReadInt16();
			int num40 = reader.ReadByte();
			if (num40 == Main.myPlayer)
			{
				Player player2 = Main.player[num40];
				for (int num41 = 0; num41 < num39; num41++)
				{
					player2.ConsumeItem(type);
				}
				player2.wireOperationsCooldown = 0;
			}
			break;
		}
		case 111:
			if (Main.netMode == 2)
			{
				BirthdayParty.ToggleManualParty();
			}
			break;
		case 112:
		{
			int num33 = reader.ReadByte();
			int num34 = reader.ReadInt32();
			int num35 = reader.ReadInt32();
			int num36 = reader.ReadByte();
			int num37 = reader.ReadInt16();
			switch (num33)
			{
			case 1:
				if (Main.netMode == 1)
				{
					WorldGen.TreeGrowFX(num34, num35, num36, num37);
				}
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(flag, -1, -1, null, num33, num34, num35, num36, num37);
				}
				break;
			case 2:
				NPC.FairyEffects(new Vector2(num34, num35), num37);
				break;
			}
			break;
		}
		case 113:
		{
			int x15 = reader.ReadInt16();
			int y14 = reader.ReadInt16();
			if (Main.netMode == 2 && !Main.snowMoon && !Main.pumpkinMoon)
			{
				if (DD2Event.WouldFailSpawningHere(x15, y14))
				{
					DD2Event.FailureMessage(whoAmI);
				}
				DD2Event.SummonCrystal(x15, y14, whoAmI);
			}
			break;
		}
		case 114:
			if (Main.netMode == 1)
			{
				DD2Event.WipeEntities();
			}
			break;
		case 116:
			if (Main.netMode == 1)
			{
				DD2Event.TimeLeftBetweenWaves = reader.ReadInt32();
			}
			break;
		case 117:
		{
			int num231 = reader.ReadByte();
			if (Main.netMode != 2 || whoAmI == num231 || (Main.player[num231].hostile && Main.player[whoAmI].hostile))
			{
				PlayerDeathReason playerDeathReason2 = PlayerDeathReason.FromReader(reader);
				int damage3 = reader.ReadInt16();
				int num232 = reader.ReadByte() - 1;
				BitsByte bitsByte18 = reader.ReadByte();
				bool flag20 = bitsByte18[0];
				bool pvp2 = bitsByte18[1];
				int num233 = reader.ReadSByte();
				Main.player[num231].Hurt(playerDeathReason2, damage3, num232, pvp2, quiet: true, flag20, num233);
				if (Main.netMode == 2)
				{
					NetMessage.SendPlayerHurt(num231, playerDeathReason2, damage3, num232, flag20, pvp2, num233, -1, whoAmI);
				}
			}
			break;
		}
		case 118:
		{
			int num220 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num220 = whoAmI;
			}
			PlayerDeathReason playerDeathReason = PlayerDeathReason.FromReader(reader);
			int num221 = reader.ReadInt16();
			int num222 = reader.ReadByte() - 1;
			bool pvp = ((BitsByte)reader.ReadByte())[0];
			Main.player[num220].KillMe(playerDeathReason, num221, num222, pvp);
			if (Main.netMode == 2)
			{
				NetMessage.SendPlayerDeath(num220, playerDeathReason, num221, num222, pvp, -1, whoAmI);
			}
			break;
		}
		case 120:
		{
			int num210 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num210 = whoAmI;
			}
			int num211 = reader.ReadByte();
			if (num211 >= 0 && num211 < EmoteID.Count && Main.netMode == 2)
			{
				EmoteBubble.NewBubble(num211, new WorldUIAnchor(Main.player[num210]), 360);
				EmoteBubble.CheckForNPCsToReactToEmoteBubble(num211, Main.player[num210]);
			}
			break;
		}
		case 121:
		{
			int num185 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num185 = whoAmI;
			}
			int num186 = reader.ReadInt32();
			int num187 = reader.ReadByte();
			bool flag17 = false;
			if (num187 >= 8)
			{
				flag17 = true;
				num187 -= 8;
			}
			if (!TileEntity.ByID.TryGetValue(num186, out var value14))
			{
				reader.ReadInt32();
				reader.ReadByte();
				break;
			}
			if (num187 >= 8)
			{
				value14 = null;
			}
			if (value14 is TEDisplayDoll tEDisplayDoll)
			{
				tEDisplayDoll.ReadItem(num187, reader, flag17);
			}
			else
			{
				reader.ReadInt32();
				reader.ReadByte();
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(flag, -1, num185, null, num185, num186, num187, flag17.ToInt());
			}
			break;
		}
		case 122:
		{
			int num156 = reader.ReadInt32();
			int num157 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num157 = whoAmI;
			}
			if (Main.netMode == 2)
			{
				if (num156 == -1)
				{
					Main.player[num157].tileEntityAnchor.Clear();
					NetMessage.TrySendData(flag, -1, -1, null, num156, num157);
					break;
				}
				if (!TileEntity.IsOccupied(num156, out var _) && TileEntity.ByID.TryGetValue(num156, out var value11))
				{
					Main.player[num157].tileEntityAnchor.Set(num156, value11.Position.X, value11.Position.Y);
					NetMessage.TrySendData(flag, -1, -1, null, num156, num157);
				}
			}
			if (Main.netMode == 1)
			{
				TileEntity value12;
				if (num156 == -1)
				{
					Main.player[num157].tileEntityAnchor.Clear();
				}
				else if (TileEntity.ByID.TryGetValue(num156, out value12))
				{
					TileEntity.SetInteractionAnchor(Main.player[num157], value12.Position.X, value12.Position.Y, num156);
				}
			}
			break;
		}
		case 123:
			if (Main.netMode == 2)
			{
				short x11 = reader.ReadInt16();
				int y10 = reader.ReadInt16();
				int netid2 = reader.ReadInt16();
				int prefix2 = reader.ReadByte();
				int stack4 = reader.ReadInt16();
				TEWeaponsRack.TryPlacing(x11, y10, netid2, prefix2, stack4);
			}
			break;
		case 124:
		{
			int num138 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num138 = whoAmI;
			}
			int num139 = reader.ReadInt32();
			int num140 = reader.ReadByte();
			bool flag13 = false;
			if (num140 >= 2)
			{
				flag13 = true;
				num140 -= 2;
			}
			if (!TileEntity.ByID.TryGetValue(num139, out var value10))
			{
				reader.ReadInt32();
				reader.ReadByte();
				break;
			}
			if (num140 >= 2)
			{
				value10 = null;
			}
			if (value10 is TEHatRack tEHatRack)
			{
				tEHatRack.ReadItem(num140, reader, flag13);
			}
			else
			{
				reader.ReadInt32();
				reader.ReadByte();
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(flag, -1, num138, null, num138, num139, num140, flag13.ToInt());
			}
			break;
		}
		case 125:
		{
			int num128 = reader.ReadByte();
			int num129 = reader.ReadInt16();
			int num130 = reader.ReadInt16();
			int num131 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num128 = whoAmI;
			}
			if (Main.netMode == 1)
			{
				Main.player[Main.myPlayer].GetOtherPlayersPickTile(num129, num130, num131);
			}
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(125, -1, num128, null, num128, num129, num130, num131);
			}
			break;
		}
		case 126:
			if (Main.netMode == 1)
			{
				NPC.RevengeManager.AddMarkerFromReader(reader);
			}
			break;
		case 127:
		{
			int markerUniqueID = reader.ReadInt32();
			if (Main.netMode == 1)
			{
				NPC.RevengeManager.DestroyMarker(markerUniqueID);
			}
			break;
		}
		case 128:
		{
			int num117 = reader.ReadByte();
			int num118 = reader.ReadUInt16();
			int num119 = reader.ReadUInt16();
			int num120 = reader.ReadUInt16();
			int num121 = reader.ReadUInt16();
			if (Main.netMode == 2)
			{
				NetMessage.SendData(128, -1, num117, null, num117, num120, num121, 0f, num118, num119);
			}
			else
			{
				GolfHelper.ContactListener.PutBallInCup_TextAndEffects(new Point(num118, num119), num117, num120, num121);
			}
			break;
		}
		case 129:
			if (Main.netMode == 1)
			{
				Main.FixUIScale();
				Main.TrySetPreparationState(Main.WorldPreparationState.ProcessingData);
			}
			break;
		case 130:
		{
			if (Main.netMode != 2)
			{
				break;
			}
			int num98 = reader.ReadUInt16();
			int num99 = reader.ReadUInt16();
			int num100 = reader.ReadInt16();
			if (num100 == 682)
			{
				if (NPC.unlockedSlimeRedSpawn)
				{
					break;
				}
				NPC.unlockedSlimeRedSpawn = true;
				NetMessage.TrySendData(7);
			}
			num98 *= 16;
			num99 *= 16;
			NPC nPC3 = new NPC();
			nPC3.SetDefaults(num100);
			int type5 = nPC3.type;
			int netID = nPC3.netID;
			int num101 = NPC.NewNPC(new EntitySource_FishedOut(Main.player[whoAmI]), num98, num99, num100);
			if (netID != type5)
			{
				Main.npc[num101].SetDefaults(netID);
				NetMessage.TrySendData(23, -1, -1, null, num101);
			}
			if (num100 == 682)
			{
				WorldGen.CheckAchievement_RealEstateAndTownSlimes();
			}
			break;
		}
		case 131:
			if (Main.netMode == 1)
			{
				int num92 = reader.ReadUInt16();
				NPC nPC = null;
				nPC = ((num92 >= 200) ? new NPC() : Main.npc[num92]);
				int num93 = reader.ReadByte();
				if (num93 == 1)
				{
					int time = reader.ReadInt32();
					int fromWho = reader.ReadInt16();
					nPC.GetImmuneTime(fromWho, time);
				}
			}
			break;
		case 132:
			if (Main.netMode == 1)
			{
				Point point = reader.ReadVector2().ToPoint();
				ushort key = reader.ReadUInt16();
				LegacySoundStyle legacySoundStyle = SoundID.SoundByIndex[key];
				BitsByte bitsByte4 = reader.ReadByte();
				int num68 = -1;
				float num69 = 1f;
				float num70 = 0f;
				SoundEngine.PlaySound(Style: (!bitsByte4[0]) ? legacySoundStyle.Style : reader.ReadInt32(), volumeScale: (!bitsByte4[1]) ? legacySoundStyle.Volume : MathHelper.Clamp(reader.ReadSingle(), 0f, 1f), pitchOffset: (!bitsByte4[2]) ? legacySoundStyle.GetRandomPitch() : MathHelper.Clamp(reader.ReadSingle(), -1f, 1f), type: legacySoundStyle.SoundId, x: point.X, y: point.Y);
			}
			break;
		case 133:
			if (Main.netMode == 2)
			{
				short x6 = reader.ReadInt16();
				int y5 = reader.ReadInt16();
				int netid = reader.ReadInt16();
				int prefix = reader.ReadByte();
				int stack = reader.ReadInt16();
				TEFoodPlatter.TryPlacing(x6, y5, netid, prefix, stack);
			}
			break;
		case 134:
		{
			int num65 = reader.ReadByte();
			int ladyBugLuckTimeLeft = reader.ReadInt32();
			float torchLuck = reader.ReadSingle();
			byte luckPotion = reader.ReadByte();
			bool hasGardenGnomeNearby = reader.ReadBoolean();
			float equipmentBasedLuckBonus = reader.ReadSingle();
			float coinLuck = reader.ReadSingle();
			if (Main.netMode == 2)
			{
				num65 = whoAmI;
			}
			Player obj2 = Main.player[num65];
			obj2.ladyBugLuckTimeLeft = ladyBugLuckTimeLeft;
			obj2.torchLuck = torchLuck;
			obj2.luckPotion = luckPotion;
			obj2.HasGardenGnomeNearby = hasGardenGnomeNearby;
			obj2.equipmentBasedLuckBonus = equipmentBasedLuckBonus;
			obj2.coinLuck = coinLuck;
			obj2.RecalculateLuck();
			if (Main.netMode == 2)
			{
				NetMessage.SendData(134, -1, num65, null, num65);
			}
			break;
		}
		case 135:
		{
			int num56 = reader.ReadByte();
			if (Main.netMode == 1)
			{
				Main.player[num56].immuneAlpha = 255;
			}
			break;
		}
		case 136:
		{
			for (int num54 = 0; num54 < 2; num54++)
			{
				for (int num55 = 0; num55 < 3; num55++)
				{
					NPC.cavernMonsterType[num54, num55] = reader.ReadUInt16();
				}
			}
			break;
		}
		case 137:
			if (Main.netMode == 2)
			{
				int num49 = reader.ReadInt16();
				int buffTypeToRemove = reader.ReadUInt16();
				if (num49 >= 0 && num49 < 200)
				{
					Main.npc[num49].RequestBuffRemoval(buffTypeToRemove);
				}
			}
			break;
		case 139:
			if (Main.netMode != 2)
			{
				int num47 = reader.ReadByte();
				bool flag8 = reader.ReadBoolean();
				Main.countsAsHostForGameplay[num47] = flag8;
			}
			break;
		case 140:
		{
			int num45 = reader.ReadByte();
			int num46 = reader.ReadInt32();
			switch (num45)
			{
			case 0:
				if (Main.netMode == 1)
				{
					CreditsRollEvent.SetRemainingTimeDirect(num46);
				}
				break;
			case 1:
				if (Main.netMode == 2)
				{
					NPC.TransformCopperSlime(num46);
				}
				break;
			case 2:
				if (Main.netMode == 2)
				{
					NPC.TransformElderSlime(num46);
				}
				break;
			}
			break;
		}
		case 141:
		{
			LucyAxeMessage.MessageSource messageSource = (LucyAxeMessage.MessageSource)reader.ReadByte();
			byte b = reader.ReadByte();
			Vector2 velocity = reader.ReadVector2();
			int num42 = reader.ReadInt32();
			int num43 = reader.ReadInt32();
			if (Main.netMode == 2)
			{
				NetMessage.SendData(141, -1, whoAmI, null, (int)messageSource, (int)b, velocity.X, velocity.Y, num42, num43);
			}
			else
			{
				LucyAxeMessage.CreateFromNet(messageSource, b, new Vector2(num42, num43), velocity);
			}
			break;
		}
		case 142:
		{
			int num38 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num38 = whoAmI;
			}
			Player obj = Main.player[num38];
			obj.piggyBankProjTracker.TryReading(reader);
			obj.voidLensChest.TryReading(reader);
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(142, -1, whoAmI, null, num38);
			}
			break;
		}
		case 143:
			if (Main.netMode == 2)
			{
				DD2Event.AttemptToSkipWaitTime();
			}
			break;
		case 144:
			if (Main.netMode == 2)
			{
				NPC.HaveDryadDoStardewAnimation();
			}
			break;
		case 146:
			switch (reader.ReadByte())
			{
			case 0:
				Item.ShimmerEffect(reader.ReadVector2());
				break;
			case 1:
			{
				Vector2 coinPosition = reader.ReadVector2();
				int coinAmount = reader.ReadInt32();
				Main.player[Main.myPlayer].AddCoinLuck(coinPosition, coinAmount);
				break;
			}
			case 2:
			{
				int num32 = reader.ReadInt32();
				Main.npc[num32].SetNetShimmerEffect();
				break;
			}
			}
			break;
		case 147:
		{
			int num26 = reader.ReadByte();
			if (Main.netMode == 2)
			{
				num26 = whoAmI;
			}
			int num27 = reader.ReadByte();
			Main.player[num26].TrySwitchingLoadout(num27);
			ReadAccessoryVisibility(reader, Main.player[num26].hideVisibleAccessory);
			if (Main.netMode == 2)
			{
				NetMessage.TrySendData(flag, -1, num26, null, num26, num27);
			}
			break;
		}
		default:
			if (Netplay.Clients[whoAmI].State == 0)
			{
				NetMessage.BootPlayer(whoAmI, Lang.mp[2].ToNetworkText());
			}
			break;
		case 15:
		case 25:
		case 26:
		case 44:
		case 67:
		case 93:
			break;
		}
	}

	private static void ReadAccessoryVisibility(BinaryReader reader, bool[] hideVisibleAccessory)
	{
		ushort num = reader.ReadUInt16();
		for (int i = 0; i < hideVisibleAccessory.Length; i++)
		{
			hideVisibleAccessory[i] = (num & (1 << i)) != 0;
		}
	}

	private static void TrySendingItemArray(int plr, Item[] array, int slotStartIndex)
	{
		for (int flag = 0; flag < array.Length; flag++)
		{
			NetMessage.TrySendData(5, -1, -1, null, plr, slotStartIndex + flag, (int)array[flag].prefix);
		}
	}
}
