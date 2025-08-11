using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.Persistence.Loaders;
using Steamworks;
using Unity.AI.Navigation;
using UnityEngine;

namespace ScheduleOne.PlayerScripts;

public class PlayerManager : Singleton<PlayerManager>, IBaseSaveable, ISaveable
{
	private PlayersLoader loader = new PlayersLoader();

	[SerializeField]
	protected List<PlayerData> loadedPlayerData = new List<PlayerData>();

	protected List<string> loadedPlayerDataPaths = new List<string>();

	protected List<string> loadedPlayerFileNames = new List<string>();

	public NavMeshSurface PlayerRecoverySurface;

	public string SaveFolderName => "Players";

	public string SaveFileName => "Players";

	public Loader Loader => loader;

	public bool ShouldSaveUnderFolder => true;

	public List<string> LocalExtraFiles { get; set; } = new List<string>();

	public List<string> LocalExtraFolders { get; set; } = new List<string>();

	public bool HasChanged { get; set; }

	protected override void Awake()
	{
		base.Awake();
		InitializeSaveable();
	}

	public virtual void InitializeSaveable()
	{
		Singleton<SaveManager>.Instance.RegisterSaveable(this);
	}

	public virtual string GetSaveString()
	{
		return string.Empty;
	}

	public virtual List<string> WriteData(string parentFolderPath)
	{
		List<string> list = new List<string>();
		string containerFolder = ((ISaveable)this).GetContainerFolder(parentFolderPath);
		int i;
		for (i = 0; i < Player.PlayerList.Count; i++)
		{
			new SaveRequest(Player.PlayerList[i], containerFolder);
			list.Add(Player.PlayerList[i].SaveFolderName);
			if (!loadedPlayerData.Exists((PlayerData PlayerData) => PlayerData.PlayerCode == Player.PlayerList[i].PlayerCode))
			{
				loadedPlayerData.Add(Player.PlayerList[i].GetPlayerData());
				loadedPlayerDataPaths.Add(Path.Combine(containerFolder, Player.PlayerList[i].SaveFolderName));
				loadedPlayerFileNames.Add(Player.PlayerList[i].SaveFolderName);
			}
		}
		string[] collection = Directory.GetDirectories(containerFolder).Select(Path.GetFileName).ToArray();
		list.AddRange(collection);
		list.AddRange(loadedPlayerFileNames);
		return list;
	}

	public void SavePlayer(Player player)
	{
		Console.Log("Saving player: " + player.PlayerCode);
		string text = Path.Combine(Singleton<LoadManager>.Instance.LoadedGameFolderPath, SaveFolderName);
		Singleton<SaveManager>.Instance.ClearCompletedSaveable(player);
		string saveString = player.GetSaveString();
		((ISaveable)player).WriteBaseData(text, saveString);
		player.WriteData(text);
		PlayerData playerData = loadedPlayerData.FirstOrDefault((PlayerData PlayerData) => PlayerData.PlayerCode == player.PlayerCode);
		if (playerData != null)
		{
			int index = loadedPlayerData.IndexOf(playerData);
			loadedPlayerData[index] = player.GetPlayerData();
		}
		else
		{
			loadedPlayerData.Add(player.GetPlayerData());
			loadedPlayerDataPaths.Add(Path.Combine(text, player.SaveFolderName));
			loadedPlayerFileNames.Add(player.SaveFolderName);
		}
	}

	public void LoadPlayer(PlayerData data, string containerPath)
	{
		loadedPlayerData.Add(data);
		loadedPlayerDataPaths.Add(containerPath);
		loadedPlayerFileNames.Add(Path.GetFileName(containerPath));
		Player player = Player.PlayerList.FirstOrDefault((Player Player) => Player.PlayerCode == data.PlayerCode);
		if (player == null && InstanceFinder.IsServer)
		{
			string fileName = Path.GetFileName(containerPath);
			if (fileName == "Player_Local" || fileName == "Player_0")
			{
				player = Player.Local;
			}
		}
		if (player != null)
		{
			player.Load(data, containerPath);
		}
	}

	public void AllPlayerFilesLoaded()
	{
		if (InstanceFinder.IsServer)
		{
			string text = string.Empty;
			if (SteamManager.Initialized)
			{
				text = SteamUser.GetSteamID().ToString();
			}
			if (loadedPlayerFileNames.Contains("Player_0"))
			{
				int index = loadedPlayerFileNames.IndexOf("Player_0");
				Player.Local.Load(loadedPlayerData[index], loadedPlayerDataPaths[index]);
			}
			else if (text != string.Empty && loadedPlayerFileNames.Contains("Player_" + text))
			{
				int index2 = loadedPlayerFileNames.IndexOf("Player_" + text);
				Player.Local.Load(loadedPlayerData[index2], loadedPlayerDataPaths[index2]);
			}
			else if (loadedPlayerFileNames.Contains("Player_Local"))
			{
				int index3 = loadedPlayerFileNames.IndexOf("Player_Local");
				Player.Local.Load(loadedPlayerData[index3], loadedPlayerDataPaths[index3]);
			}
			else if (loadedPlayerData.Count > 0)
			{
				Player.Local.Load(loadedPlayerData[0], loadedPlayerDataPaths[0]);
			}
			else
			{
				Console.LogWarning("Couldn't find any data for host player. This is fine if this is a new game, but not if this is a loaded game.");
			}
		}
	}

	public bool TryGetPlayerData(string playerCode, out PlayerData data, out string inventoryString, out string appearanceString, out string clothingString, out VariableData[] variables)
	{
		data = loadedPlayerData.FirstOrDefault((PlayerData PlayerData) => PlayerData.PlayerCode == playerCode);
		inventoryString = string.Empty;
		appearanceString = string.Empty;
		clothingString = string.Empty;
		variables = null;
		List<VariableData> list = new List<VariableData>();
		if (data != null)
		{
			string text = loadedPlayerDataPaths[loadedPlayerData.IndexOf(data)];
			PlayerLoader playerLoader = new PlayerLoader();
			if (playerLoader.TryLoadFile(text, "Inventory", out var contents))
			{
				inventoryString = contents;
			}
			else
			{
				Console.LogWarning("Failed to load player inventory under " + text);
			}
			if (playerLoader.TryLoadFile(text, "Appearance", out var contents2))
			{
				appearanceString = contents2;
			}
			else
			{
				Console.LogWarning("Failed to load player appearance under " + text);
			}
			string path = Path.Combine(text, "Variables");
			if (Directory.Exists(path))
			{
				string[] files = Directory.GetFiles(path);
				VariablesLoader variablesLoader = new VariablesLoader();
				for (int num = 0; num < files.Length; num++)
				{
					if (variablesLoader.TryLoadFile(files[num], out var contents3, autoAddExtension: false))
					{
						VariableData item = null;
						try
						{
							item = JsonUtility.FromJson<VariableData>(contents3);
						}
						catch (Exception ex)
						{
							Debug.LogError("Error loading player variable data: " + ex.Message);
						}
						if (data != null)
						{
							list.Add(item);
						}
					}
				}
			}
		}
		if (list.Count > 0)
		{
			variables = list.ToArray();
		}
		return data != null;
	}
}
