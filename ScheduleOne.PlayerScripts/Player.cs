using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using EasyButtons;
using FishNet;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using FishNet.Serializing.Generated;
using FishNet.Transporting;
using FishySteamworks;
using ScheduleOne.Audio;
using ScheduleOne.AvatarFramework;
using ScheduleOne.AvatarFramework.Animation;
using ScheduleOne.AvatarFramework.Customization;
using ScheduleOne.Combat;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.ItemFramework;
using ScheduleOne.Law;
using ScheduleOne.Map;
using ScheduleOne.Money;
using ScheduleOne.Networking;
using ScheduleOne.Persistence;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.Persistence.Loaders;
using ScheduleOne.PlayerScripts.Health;
using ScheduleOne.Product;
using ScheduleOne.Property;
using ScheduleOne.Skating;
using ScheduleOne.Stealth;
using ScheduleOne.Tools;
using ScheduleOne.UI;
using ScheduleOne.UI.MainMenu;
using ScheduleOne.Variables;
using ScheduleOne.Vehicles;
using Steamworks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ScheduleOne.PlayerScripts;

public class Player : NetworkBehaviour, ISaveable, IDamageable
{
	public delegate void VehicleEvent(LandVehicle vehicle);

	public delegate void VehicleTransformEvent(LandVehicle vehicle, Transform exitPoint);

	public const string OWNER_PLAYER_CODE = "Local";

	public const float CapColDefaultHeight = 2f;

	public List<NetworkObject> objectsTemporarilyOwnedByPlayer = new List<NetworkObject>();

	public static Action onLocalPlayerSpawned;

	public static Action<Player> onPlayerSpawned;

	public static Action<Player> onPlayerDespawned;

	public static Player Local;

	public static List<Player> PlayerList = new List<Player>();

	[Header("References")]
	public GameObject LocalGameObject;

	public ScheduleOne.AvatarFramework.Avatar Avatar;

	public AvatarAnimation Anim;

	public SmoothedVelocityCalculator VelocityCalculator;

	public Transform EyePosition;

	public AvatarSettings TestAvatarSettings;

	public PlayerVisualState VisualState;

	public PlayerVisibility Visibility;

	public CapsuleCollider CapCol;

	public POI PoI;

	public PlayerHealth Health;

	public PlayerCrimeData CrimeData;

	public PlayerEnergy Energy;

	public ParticleSystem ZapParticles;

	public AudioSourceController ZapSound;

	public Transform MimicCamera;

	public AvatarFootstepDetector FootstepDetector;

	public LocalPlayerFootstepGenerator LocalFootstepDetector;

	public CharacterController CharacterController;

	public AudioSourceController PunchSound;

	public OptimizedLight ThirdPersonFlashlight;

	public WorldspaceDialogueRenderer NameLabel;

	public PlayerClothing Clothing;

	[Header("Settings")]
	public LayerMask GroundDetectionMask;

	public float AvatarOffset_Standing = -0.97f;

	public float AvatarOffset_Crouched = -0.45f;

	[Header("Movement mapping")]
	public AnimationCurve WalkingMapCurve;

	public AnimationCurve CrouchWalkMapCurve;

	[CompilerGenerated]
	[SyncVar(WritePermissions = WritePermission.ClientUnsynchronized)]
	public string _003CPlayerName_003Ek__BackingField;

	public NetworkConnection Connection;

	[CompilerGenerated]
	[SyncVar(WritePermissions = WritePermission.ClientUnsynchronized)]
	public string _003CPlayerCode_003Ek__BackingField;

	[CompilerGenerated]
	[SyncVar(OnChange = "CurrentVehicleChanged")]
	public NetworkObject _003CCurrentVehicle_003Ek__BackingField;

	public VehicleEvent onEnterVehicle;

	public VehicleTransformEvent onExitVehicle;

	public LandVehicle LastDrivenVehicle;

	[CompilerGenerated]
	[SyncVar]
	public NetworkObject _003CCurrentBed_003Ek__BackingField;

	[CompilerGenerated]
	[SyncVar]
	public bool _003CIsReadyToSleep_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsSkating_003Ek__BackingField;

	public Action<Skateboard> onSkateboardMounted;

	public Action onSkateboardDismounted;

	public bool HasCompletedIntro;

	[CompilerGenerated]
	[SyncVar(Channel = Channel.Unreliable, SendRate = 0.1f)]
	public Vector3 _003CCameraPosition_003Ek__BackingField;

	[CompilerGenerated]
	[SyncVar(Channel = Channel.Unreliable, SendRate = 0.1f)]
	public Quaternion _003CCameraRotation_003Ek__BackingField;

	public ItemSlot[] Inventory = new ItemSlot[9];

	[Header("Appearance debugging")]
	public BasicAvatarSettings DebugAvatarSettings;

	private PlayerLoader loader = new PlayerLoader();

	public UnityEvent onRagdoll;

	public UnityEvent onRagdollEnd;

	public UnityEvent onArrested;

	public UnityEvent onFreed;

	public UnityEvent onTased;

	public UnityEvent onTasedEnd;

	public UnityEvent onPassedOut;

	public UnityEvent onPassOutRecovery;

	public List<BaseVariable> PlayerVariables = new List<BaseVariable>();

	public Dictionary<string, BaseVariable> VariableDict = new Dictionary<string, BaseVariable>();

	private float standingScale = 1f;

	private float timeAirborne;

	private Coroutine taseCoroutine;

	private List<ConstantForce> ragdollForceComponents = new List<ConstantForce>();

	private List<int> impactHistory = new List<int>();

	public SyncVar<string> syncVar____003CPlayerName_003Ek__BackingField;

	public SyncVar<string> syncVar____003CPlayerCode_003Ek__BackingField;

	public SyncVar<NetworkObject> syncVar____003CCurrentVehicle_003Ek__BackingField;

	public SyncVar<NetworkObject> syncVar____003CCurrentBed_003Ek__BackingField;

	public SyncVar<bool> syncVar____003CIsReadyToSleep_003Ek__BackingField;

	public SyncVar<Vector3> syncVar____003CCameraPosition_003Ek__BackingField;

	public SyncVar<Quaternion> syncVar____003CCameraRotation_003Ek__BackingField;

	private bool NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerAssembly_002DCSharp_002Edll_Excuted;

	public bool IsLocalPlayer => base.IsOwner;

	public string PlayerName
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CPlayerName_003Ek__BackingField;
		}
		[CompilerGenerated]
		protected set
		{
			this.sync___set_value__003CPlayerName_003Ek__BackingField(value, asServer: true);
		}
	} = "Player";

	public string PlayerCode
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CPlayerCode_003Ek__BackingField;
		}
		[CompilerGenerated]
		protected set
		{
			this.sync___set_value__003CPlayerCode_003Ek__BackingField(value, asServer: true);
		}
	} = string.Empty;

	public NetworkObject CurrentVehicle
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CCurrentVehicle_003Ek__BackingField;
		}
		[CompilerGenerated]
		[ServerRpc(RunLocally = true)]
		set
		{
			RpcWriter___Server_set_CurrentVehicle_3323014238(value);
			RpcLogic___set_CurrentVehicle_3323014238(value);
		}
	}

	public float TimeSinceVehicleExit { get; protected set; }

	public bool Crouched { get; private set; }

	public NetworkObject CurrentBed
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CCurrentBed_003Ek__BackingField;
		}
		[CompilerGenerated]
		[ServerRpc]
		set
		{
			RpcWriter___Server_set_CurrentBed_3323014238(value);
		}
	}

	public bool IsReadyToSleep
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CIsReadyToSleep_003Ek__BackingField;
		}
		[CompilerGenerated]
		private set
		{
			this.sync___set_value__003CIsReadyToSleep_003Ek__BackingField(value, asServer: true);
		}
	}

	public bool IsSkating
	{
		[CompilerGenerated]
		get
		{
			return _003CIsSkating_003Ek__BackingField;
		}
		[CompilerGenerated]
		[ServerRpc]
		set
		{
			RpcWriter___Server_set_IsSkating_1140765316(value);
		}
	}

	public Skateboard ActiveSkateboard { get; private set; }

	public bool IsSleeping { get; protected set; }

	public bool IsRagdolled { get; protected set; }

	public bool IsArrested { get; protected set; }

	public bool IsTased { get; protected set; }

	public bool IsUnconscious { get; protected set; }

	public ScheduleOne.Property.Property CurrentProperty { get; protected set; }

	public ScheduleOne.Property.Property LastVisitedProperty { get; protected set; }

	public Business CurrentBusiness { get; protected set; }

	public Vector3 PlayerBasePosition => base.transform.position - base.transform.up * (CharacterController.height / 2f);

	public Vector3 CameraPosition
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CCameraPosition_003Ek__BackingField;
		}
		[CompilerGenerated]
		[ServerRpc]
		set
		{
			RpcWriter___Server_set_CameraPosition_4276783012(value);
		}
	} = Vector3.zero;

	public Quaternion CameraRotation
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CCameraRotation_003Ek__BackingField;
		}
		[CompilerGenerated]
		[ServerRpc]
		set
		{
			RpcWriter___Server_set_CameraRotation_3429297120(value);
		}
	} = Quaternion.identity;

	public BasicAvatarSettings CurrentAvatarSettings { get; protected set; }

	public ProductItemInstance ConsumedProduct { get; private set; }

	public int TimeSinceProductConsumed { get; private set; }

	public string SaveFolderName
	{
		get
		{
			if (InstanceFinder.IsServer && base.IsOwner)
			{
				return "Player_0";
			}
			return "Player_" + SyncAccessor__003CPlayerCode_003Ek__BackingField;
		}
	}

	public string SaveFileName => "Player";

	public Loader Loader => loader;

	public bool ShouldSaveUnderFolder => true;

	public List<string> LocalExtraFiles { get; set; } = new List<string> { "Inventory", "Appearance", "Clothing" };

	public List<string> LocalExtraFolders { get; set; } = new List<string> { "Variables" };

	public bool HasChanged { get; set; }

	public bool avatarVisibleToLocalPlayer { get; private set; }

	public bool playerDataRetrieveReturned { get; private set; }

	public bool playerSaveRequestReturned { get; private set; }

	public bool PlayerInitializedOverNetwork { get; private set; }

	public bool Paranoid { get; set; }

	public bool Sneaky { get; set; }

	public bool Disoriented { get; set; }

	public string SyncAccessor__003CPlayerName_003Ek__BackingField
	{
		get
		{
			return PlayerName;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				PlayerName = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CPlayerName_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public string SyncAccessor__003CPlayerCode_003Ek__BackingField
	{
		get
		{
			return PlayerCode;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				PlayerCode = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CPlayerCode_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public NetworkObject SyncAccessor__003CCurrentVehicle_003Ek__BackingField
	{
		get
		{
			return CurrentVehicle;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				CurrentVehicle = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CCurrentVehicle_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public NetworkObject SyncAccessor__003CCurrentBed_003Ek__BackingField
	{
		get
		{
			return CurrentBed;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				CurrentBed = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CCurrentBed_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public bool SyncAccessor__003CIsReadyToSleep_003Ek__BackingField
	{
		get
		{
			return IsReadyToSleep;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				IsReadyToSleep = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CIsReadyToSleep_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public Vector3 SyncAccessor__003CCameraPosition_003Ek__BackingField
	{
		get
		{
			return CameraPosition;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				CameraPosition = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CCameraPosition_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public Quaternion SyncAccessor__003CCameraRotation_003Ek__BackingField
	{
		get
		{
			return CameraRotation;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				CameraRotation = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CCameraRotation_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	[Button]
	public void LoadDebugAvatarSettings()
	{
		SetAppearance(DebugAvatarSettings);
	}

	public static Player GetPlayer(NetworkConnection conn)
	{
		for (int i = 0; i < PlayerList.Count; i++)
		{
			if (PlayerList[i].Connection == conn)
			{
				return PlayerList[i];
			}
		}
		return null;
	}

	public static Player GetRandomPlayer(bool excludeArrestedOrDead = true, bool excludeSleeping = true)
	{
		List<Player> list = new List<Player>();
		for (int i = 0; i < PlayerList.Count; i++)
		{
			if ((!excludeArrestedOrDead || (!PlayerList[i].IsArrested && PlayerList[i].Health.IsAlive)) && (!excludeSleeping || !PlayerList[i].IsSleeping))
			{
				list.Add(PlayerList[i]);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		int index = UnityEngine.Random.Range(0, list.Count);
		return list[index];
	}

	public static Player GetPlayer(string playerCode)
	{
		return PlayerList.Find((Player x) => x.SyncAccessor__003CPlayerCode_003Ek__BackingField == playerCode);
	}

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_ScheduleOne_002EPlayerScripts_002EPlayer_Assembly_002DCSharp_002Edll();
		NetworkInitialize__Late();
	}

	public virtual void InitializeSaveable()
	{
		Singleton<SaveManager>.Instance.RegisterSaveable(this);
	}

	protected virtual void Start()
	{
		MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
		instance.onNetworthCalculation = (Action<MoneyManager.FloatContainer>)Delegate.Combine(instance.onNetworthCalculation, new Action<MoneyManager.FloatContainer>(GetNetworth));
		TimeManager instance2 = NetworkSingleton<ScheduleOne.GameTime.TimeManager>.Instance;
		instance2.onMinutePass = (Action)Delegate.Combine(instance2.onMinutePass, new Action(MinPass));
	}

	protected virtual void OnDestroy()
	{
		if (NetworkSingleton<ScheduleOne.GameTime.TimeManager>.InstanceExists)
		{
			TimeManager instance = NetworkSingleton<ScheduleOne.GameTime.TimeManager>.Instance;
			instance.onMinutePass = (Action)Delegate.Remove(instance.onMinutePass, new Action(MinPass));
		}
		if (NetworkSingleton<MoneyManager>.InstanceExists)
		{
			MoneyManager instance2 = NetworkSingleton<MoneyManager>.Instance;
			instance2.onNetworthCalculation = (Action<MoneyManager.FloatContainer>)Delegate.Remove(instance2.onNetworthCalculation, new Action<MoneyManager.FloatContainer>(GetNetworth));
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		Connection = base.Owner;
		if (base.IsOwner)
		{
			if (Application.isEditor)
			{
				LoadDebugAvatarSettings();
			}
			LocalGameObject.gameObject.SetActive(value: true);
			Local = this;
			if (onLocalPlayerSpawned != null)
			{
				onLocalPlayerSpawned();
			}
			LayerUtility.SetLayerRecursively(Avatar.gameObject, LayerMask.NameToLayer("Invisible"));
			if (Singleton<Lobby>.Instance.IsInLobby && !Singleton<Lobby>.Instance.IsHost)
			{
				InstanceFinder.TransportManager.GetTransport<global::FishySteamworks.FishySteamworks>().OnClientConnectionState += ClientConnectionStateChanged;
			}
			FootstepDetector.enabled = false;
			PoI.SetMainText("You");
			if (PoI.UI != null)
			{
				PoI.UI.GetComponentInChildren<Animation>().Play();
			}
			NameLabel.gameObject.SetActive(value: false);
			if (base.IsHost)
			{
				if (Singleton<LoadManager>.Instance.IsGameLoaded)
				{
					PlayerLoaded();
				}
				else
				{
					Singleton<LoadManager>.Instance.onLoadComplete.AddListener(PlayerLoaded);
				}
			}
			CSteamID cSteamID = CSteamID.Nil;
			if (SteamManager.Initialized)
			{
				cSteamID = SteamUser.GetSteamID();
				PlayerName = SteamFriends.GetPersonaName();
			}
			SendPlayerNameData(SyncAccessor__003CPlayerName_003Ek__BackingField, cSteamID.m_SteamID);
			if (!InstanceFinder.IsServer)
			{
				RequestPlayerData(SyncAccessor__003CPlayerCode_003Ek__BackingField);
			}
		}
		else
		{
			LocalFootstepDetector.enabled = false;
			CapCol.isTrigger = true;
			base.gameObject.name = SyncAccessor__003CPlayerName_003Ek__BackingField + " (" + SyncAccessor__003CPlayerCode_003Ek__BackingField + ")";
			PoI.SetMainText(SyncAccessor__003CPlayerName_003Ek__BackingField);
		}
		if (base.IsOwner || InstanceFinder.IsServer || (Singleton<Lobby>.Instance.IsInLobby && Singleton<Lobby>.Instance.IsHost))
		{
			CreatePlayerVariables();
		}
		if (onPlayerSpawned != null)
		{
			onPlayerSpawned(this);
		}
		Console.Log("Player spawned (" + SyncAccessor__003CPlayerName_003Ek__BackingField + ")");
		CrimeData.RecordLastKnownPosition(resetTimeSinceSighted: false);
		if (!base.IsOwner && SyncAccessor__003CCurrentVehicle_003Ek__BackingField != null)
		{
			Console.Log("This player is in a vehicle!");
			EnterVehicle(SyncAccessor__003CCurrentVehicle_003Ek__BackingField.GetComponent<LandVehicle>());
		}
		PlayerList.Add(this);
	}

	private void PlayerLoaded()
	{
		Singleton<LoadManager>.Instance.onLoadComplete.RemoveListener(PlayerLoaded);
		if (!base.IsOwner)
		{
			return;
		}
		if (PoI != null)
		{
			PoI.SetMainText("You");
			if (PoI.UI != null)
			{
				PoI.UI.GetComponentInChildren<Animation>().Play();
			}
		}
		MarkPlayerInitialized();
		if (!HasCompletedIntro && !Singleton<LoadManager>.Instance.DebugMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main")
		{
			PlayerSingleton<PlayerMovement>.Instance.Teleport(NetworkSingleton<GameManager>.Instance.SpawnPoint.position);
			base.transform.forward = NetworkSingleton<GameManager>.Instance.SpawnPoint.forward;
			Console.Log("Player has not completed intro; playing intro");
			Singleton<DemoIntro>.Instance.Play();
			Singleton<CharacterCreator>.Instance.onComplete.AddListener(MarkIntroCompleted);
		}
	}

	public override void OnSpawnServer(NetworkConnection connection)
	{
		base.OnSpawnServer(connection);
		if (base.Owner != connection)
		{
			PlayerData data = new PlayerData(SyncAccessor__003CPlayerCode_003Ek__BackingField, base.transform.position, base.transform.eulerAngles.y, HasCompletedIntro);
			string empty = string.Empty;
			string appearanceString = ((CurrentAvatarSettings != null) ? CurrentAvatarSettings.GetJson() : string.Empty);
			string clothingString = GetClothingString();
			if (Crouched)
			{
				ReceiveCrouched(connection, crouched: true);
			}
			ReceivePlayerData(connection, data, empty, appearanceString, clothingString, null);
			ReceivePlayerNameData(connection, SyncAccessor__003CPlayerName_003Ek__BackingField, SyncAccessor__003CPlayerCode_003Ek__BackingField);
		}
	}

	[ServerRpc(RunLocally = true, RequireOwnership = false)]
	public void RequestSavePlayer()
	{
		RpcWriter___Server_RequestSavePlayer_2166136261();
		RpcLogic___RequestSavePlayer_2166136261();
	}

	[ObserversRpc]
	[TargetRpc]
	private void ReturnSaveRequest(NetworkConnection conn, bool successful)
	{
		if ((object)conn == null)
		{
			RpcWriter___Observers_ReturnSaveRequest_214505783(conn, successful);
		}
		else
		{
			RpcWriter___Target_ReturnSaveRequest_214505783(conn, successful);
		}
	}

	[ObserversRpc(RunLocally = true)]
	public void HostExitedGame()
	{
		RpcWriter___Observers_HostExitedGame_2166136261();
		RpcLogic___HostExitedGame_2166136261();
	}

	private void ClientConnectionStateChanged(ClientConnectionStateArgs args)
	{
		Console.Log("Client connection state changed: " + args.ConnectionState);
		if (args.ConnectionState == LocalConnectionState.Stopping || args.ConnectionState == LocalConnectionState.Stopped)
		{
			HostExitedGame();
		}
	}

	[ServerRpc(RunLocally = true)]
	public void SendPlayerNameData(string playerName, ulong id)
	{
		RpcWriter___Server_SendPlayerNameData_586648380(playerName, id);
		RpcLogic___SendPlayerNameData_586648380(playerName, id);
	}

	[ServerRpc(RequireOwnership = false)]
	public void RequestPlayerData(string playerCode)
	{
		RpcWriter___Server_RequestPlayerData_3615296227(playerCode);
	}

	[ServerRpc(RunLocally = true)]
	public void MarkPlayerInitialized()
	{
		RpcWriter___Server_MarkPlayerInitialized_2166136261();
		RpcLogic___MarkPlayerInitialized_2166136261();
	}

	[ObserversRpc(RunLocally = true)]
	[TargetRpc]
	public void ReceivePlayerData(NetworkConnection conn, PlayerData data, string inventoryString, string appearanceString, string clothigString, VariableData[] vars)
	{
		if ((object)conn == null)
		{
			RpcWriter___Observers_ReceivePlayerData_3244732873(conn, data, inventoryString, appearanceString, clothigString, vars);
			RpcLogic___ReceivePlayerData_3244732873(conn, data, inventoryString, appearanceString, clothigString, vars);
		}
		else
		{
			RpcWriter___Target_ReceivePlayerData_3244732873(conn, data, inventoryString, appearanceString, clothigString, vars);
		}
	}

	public void SetGravityMultiplier(float multiplier)
	{
		if (base.IsOwner)
		{
			PlayerMovement.GravityMultiplier = multiplier;
		}
		foreach (ConstantForce ragdollForceComponent in ragdollForceComponents)
		{
			ragdollForceComponent.force = Physics.gravity * multiplier * ragdollForceComponent.GetComponent<Rigidbody>().mass;
		}
	}

	[ObserversRpc(RunLocally = true)]
	[TargetRpc]
	private void ReceivePlayerNameData(NetworkConnection conn, string playerName, string id)
	{
		if ((object)conn == null)
		{
			RpcWriter___Observers_ReceivePlayerNameData_3895153758(conn, playerName, id);
			RpcLogic___ReceivePlayerNameData_3895153758(conn, playerName, id);
		}
		else
		{
			RpcWriter___Target_ReceivePlayerNameData_3895153758(conn, playerName, id);
		}
	}

	public void SendFlashlightOn(bool on)
	{
		SendFlashlightOnNetworked(on);
	}

	[ServerRpc(RunLocally = true, RequireOwnership = false)]
	private void SendFlashlightOnNetworked(bool on)
	{
		RpcWriter___Server_SendFlashlightOnNetworked_1140765316(on);
		RpcLogic___SendFlashlightOnNetworked_1140765316(on);
	}

	[ObserversRpc(RunLocally = true)]
	private void SetFlashlightOn(bool on)
	{
		RpcWriter___Observers_SetFlashlightOn_1140765316(on);
		RpcLogic___SetFlashlightOn_1140765316(on);
	}

	public override void OnStopClient()
	{
		base.OnStopClient();
		PlayerList.Remove(this);
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		base.ServerManager.Objects.OnPreDestroyClientObjects += PreDestroyClientObjects;
	}

	protected virtual void Update()
	{
		HasChanged = true;
		if (SyncAccessor__003CCurrentVehicle_003Ek__BackingField != null)
		{
			TimeSinceVehicleExit = 0f;
		}
		else
		{
			TimeSinceVehicleExit += Time.deltaTime;
		}
		if (!base.IsOwner)
		{
			return;
		}
		if (base.transform.position.y < -20f)
		{
			float y = 0f;
			if (MapHeightSampler.Sample(base.transform.position.x, out y, base.transform.position.z))
			{
				PlayerSingleton<PlayerMovement>.Instance.Teleport(new Vector3(base.transform.position.x, y, base.transform.position.z));
			}
			else
			{
				PlayerSingleton<PlayerMovement>.Instance.Teleport(MapHeightSampler.ResetPosition);
			}
		}
		if (ActiveSkateboard != null)
		{
			SetCapsuleColliderHeight(1f - ActiveSkateboard.Animation.CurrentCrouchShift * 0.3f);
		}
		if (NetworkSingleton<VariableDatabase>.InstanceExists)
		{
			NetworkSingleton<VariableDatabase>.Instance.SetVariableValue("Player_In_Vehicle", (SyncAccessor__003CCurrentVehicle_003Ek__BackingField != null).ToString());
		}
	}

	protected virtual void MinPass()
	{
		if (ConsumedProduct != null)
		{
			TimeSinceProductConsumed++;
			if (TimeSinceProductConsumed >= (ConsumedProduct.Definition as ProductDefinition).EffectsDuration)
			{
				ClearProduct();
			}
		}
	}

	protected virtual void LateUpdate()
	{
		ApplyMovementVisuals();
		if (base.IsOwner)
		{
			RpcWriter___Server_set_CameraPosition_4276783012(PlayerSingleton<PlayerCamera>.Instance.transform.position);
			RpcWriter___Server_set_CameraRotation_3429297120(PlayerSingleton<PlayerCamera>.Instance.transform.rotation);
		}
		MimicCamera.transform.position = SyncAccessor__003CCameraPosition_003Ek__BackingField;
		MimicCamera.transform.rotation = SyncAccessor__003CCameraRotation_003Ek__BackingField;
	}

	private void RecalculateCurrentProperty()
	{
		ScheduleOne.Property.Property property = ScheduleOne.Property.Property.Properties.OrderBy((ScheduleOne.Property.Property x) => Vector3.Distance(x.BoundingBox.transform.position, Avatar.CenterPoint)).FirstOrDefault();
		Business business = Business.Businesses.OrderBy((Business x) => Vector3.Distance(x.BoundingBox.transform.position, Avatar.CenterPoint)).FirstOrDefault();
		if (property == null)
		{
			CurrentProperty = null;
		}
		else if (property.DoBoundsContainPoint(Avatar.CenterPoint))
		{
			CurrentProperty = property;
			LastVisitedProperty = CurrentProperty;
		}
		else
		{
			CurrentProperty = null;
		}
		if (business == null)
		{
			CurrentBusiness = null;
		}
		else if (business.DoBoundsContainPoint(Avatar.CenterPoint))
		{
			CurrentBusiness = business;
		}
		else
		{
			CurrentBusiness = null;
		}
	}

	private void ApplyMovementVisuals()
	{
		if (IsSkating)
		{
			Anim.SetTimeAirborne(0f);
			Anim.SetGrounded(grounded: true);
			Anim.SetDirection(0f);
			Anim.SetStrafe(0f);
			return;
		}
		if (GetIsGrounded())
		{
			timeAirborne = 0f;
		}
		else
		{
			timeAirborne += Time.deltaTime;
		}
		Anim.SetTimeAirborne(timeAirborne);
		if (Crouched)
		{
			standingScale = Mathf.MoveTowards(standingScale, 0f, Time.deltaTime / PlayerMovement.CrouchTime);
		}
		else
		{
			standingScale = Mathf.MoveTowards(standingScale, 1f, Time.deltaTime / PlayerMovement.CrouchTime);
		}
		Anim.SetGrounded(GetIsGrounded());
		Anim.SetCrouched(Crouched);
		Avatar.transform.localPosition = new Vector3(0f, Mathf.Lerp(AvatarOffset_Crouched, AvatarOffset_Standing, standingScale), 0f);
		Vector3 vector = base.transform.InverseTransformVector(VelocityCalculator.Velocity) / (PlayerMovement.WalkSpeed * PlayerMovement.SprintMultiplier);
		if (Crouched)
		{
			Anim.SetDirection(CrouchWalkMapCurve.Evaluate(Mathf.Abs(vector.z)) * Mathf.Sign(vector.z));
			Anim.SetStrafe(CrouchWalkMapCurve.Evaluate(Mathf.Abs(vector.x)) * Mathf.Sign(vector.x));
		}
		else
		{
			Anim.SetDirection(WalkingMapCurve.Evaluate(Mathf.Abs(vector.z)) * Mathf.Sign(vector.z));
			Anim.SetStrafe(WalkingMapCurve.Evaluate(Mathf.Abs(vector.x)) * Mathf.Sign(vector.x));
		}
	}

	public void SetVisible(bool vis, bool network = false)
	{
		Avatar.SetVisible(vis);
		CapCol.enabled = vis;
		if (network)
		{
			SetVisible_Networked(vis);
		}
	}

	[ObserversRpc]
	public void PlayJumpAnimation()
	{
		RpcWriter___Observers_PlayJumpAnimation_2166136261();
	}

	public bool GetIsGrounded()
	{
		float maxDistance = PlayerMovement.StandingControllerHeight * (Crouched ? PlayerMovement.CrouchHeightMultiplier : 1f) / 2f + 0.1f;
		RaycastHit hitInfo;
		return Physics.SphereCast(base.transform.position, PlayerMovement.ControllerRadius * 0.75f, Vector3.down, out hitInfo, maxDistance, GroundDetectionMask);
	}

	[ServerRpc(RunLocally = true, RequireOwnership = false)]
	public void SendCrouched(bool crouched)
	{
		RpcWriter___Server_SendCrouched_1140765316(crouched);
		RpcLogic___SendCrouched_1140765316(crouched);
	}

	public void SetCrouchedLocal(bool crouched)
	{
		Crouched = crouched;
	}

	[TargetRpc]
	[ObserversRpc(RunLocally = true)]
	private void ReceiveCrouched(NetworkConnection conn, bool crouched)
	{
		if ((object)conn == null)
		{
			RpcWriter___Observers_ReceiveCrouched_214505783(conn, crouched);
			RpcLogic___ReceiveCrouched_214505783(conn, crouched);
		}
		else
		{
			RpcWriter___Target_ReceiveCrouched_214505783(conn, crouched);
		}
	}

	[ServerRpc(RunLocally = true)]
	public void SendAvatarSettings(AvatarSettings settings)
	{
		RpcWriter___Server_SendAvatarSettings_4281687581(settings);
		RpcLogic___SendAvatarSettings_4281687581(settings);
	}

	[ObserversRpc(BufferLast = true, RunLocally = true)]
	public void SetAvatarSettings(AvatarSettings settings)
	{
		RpcWriter___Observers_SetAvatarSettings_4281687581(settings);
		RpcLogic___SetAvatarSettings_4281687581(settings);
	}

	[ObserversRpc]
	private void SetVisible_Networked(bool vis)
	{
		RpcWriter___Observers_SetVisible_Networked_1140765316(vis);
	}

	public void EnterVehicle(LandVehicle vehicle)
	{
		CurrentVehicle = vehicle.NetworkObject;
		LastDrivenVehicle = vehicle;
		Avatar.transform.SetParent(vehicle.transform);
		Avatar.transform.localPosition = Vector3.zero;
		Avatar.transform.localRotation = Quaternion.identity;
		if (onEnterVehicle != null)
		{
			onEnterVehicle(vehicle);
		}
		SetVisible(vis: false, network: true);
	}

	public void ExitVehicle(Transform exitPoint)
	{
		if (!(SyncAccessor__003CCurrentVehicle_003Ek__BackingField == null))
		{
			Avatar.transform.SetParent(base.transform);
			Avatar.transform.localPosition = Vector3.zero;
			Avatar.transform.localRotation = Quaternion.identity;
			Local.transform.position = exitPoint.position;
			Local.transform.rotation = exitPoint.rotation;
			Local.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
			GetComponent<NetworkTransform>().ClearReplicateCache();
			if (onExitVehicle != null)
			{
				onExitVehicle(SyncAccessor__003CCurrentVehicle_003Ek__BackingField.GetComponent<LandVehicle>(), exitPoint);
			}
			SetVisible(vis: true);
			CurrentVehicle = null;
		}
	}

	private void PreDestroyClientObjects(NetworkConnection conn)
	{
		if (SyncAccessor__003CCurrentVehicle_003Ek__BackingField != null)
		{
			SyncAccessor__003CCurrentVehicle_003Ek__BackingField.RemoveOwnership();
			SyncAccessor__003CCurrentVehicle_003Ek__BackingField.GetComponent<LandVehicle>().ExitVehicle();
		}
		int count = objectsTemporarilyOwnedByPlayer.Count;
		for (int i = 0; i < count; i++)
		{
			Debug.Log("Stripping object ownership back to server: " + objectsTemporarilyOwnedByPlayer[i].gameObject.name);
			objectsTemporarilyOwnedByPlayer[i].RemoveOwnership();
		}
	}

	private void CurrentVehicleChanged(NetworkObject oldVeh, NetworkObject newVeh, bool asServer)
	{
		if (!base.IsOwner && !(oldVeh == newVeh))
		{
			if (newVeh != null)
			{
				Avatar.transform.SetParent(newVeh.transform);
				Avatar.transform.localPosition = Vector3.zero;
				Avatar.transform.localRotation = Quaternion.identity;
				SetVisible(vis: false);
			}
			else
			{
				Avatar.transform.SetParent(base.transform);
				Avatar.transform.localPosition = Vector3.zero;
				Avatar.transform.localRotation = Quaternion.identity;
				SetVisible(vis: true);
			}
		}
	}

	public static bool AreAllPlayersReadyToSleep()
	{
		if (PlayerList.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < PlayerList.Count; i++)
		{
			if (!(PlayerList[i] == null) && !PlayerList[i].SyncAccessor__003CIsReadyToSleep_003Ek__BackingField)
			{
				return false;
			}
		}
		return true;
	}

	private void SleepStart()
	{
		IsSleeping = true;
		ClearProduct();
	}

	[ServerRpc(RunLocally = true, RequireOwnership = false)]
	public void SetReadyToSleep(bool ready)
	{
		RpcWriter___Server_SetReadyToSleep_1140765316(ready);
		RpcLogic___SetReadyToSleep_1140765316(ready);
	}

	private void SleepEnd(int minsSlept)
	{
		IsSleeping = false;
	}

	public static void Activate()
	{
		PlayerSingleton<PlayerCamera>.Instance.SetCanLook(c: true);
		PlayerSingleton<PlayerMovement>.Instance.canMove = true;
		PlayerSingleton<PlayerInventory>.Instance.SetInventoryEnabled(enabled: true);
		Singleton<HUD>.Instance.SetCrosshairVisible(vis: true);
		PlayerSingleton<PlayerCamera>.Instance.LockMouse();
	}

	public static void Deactivate(bool freeMouse)
	{
		PlayerSingleton<PlayerCamera>.Instance.SetCanLook(c: false);
		PlayerSingleton<PlayerCamera>.Instance.ResetRotation();
		PlayerSingleton<PlayerMovement>.Instance.canMove = false;
		PlayerSingleton<PlayerInventory>.Instance.SetInventoryEnabled(enabled: false);
		if (freeMouse)
		{
			PlayerSingleton<PlayerCamera>.Instance.FreeMouse();
		}
	}

	public void ExitAll()
	{
		if (SyncAccessor__003CCurrentVehicle_003Ek__BackingField != null)
		{
			SyncAccessor__003CCurrentVehicle_003Ek__BackingField.GetComponent<LandVehicle>().ExitVehicle();
			SetVisible(vis: true);
		}
		Singleton<GameInput>.Instance.ExitAll();
		PlayerSingleton<PlayerInventory>.Instance.SetInventoryEnabled(enabled: false);
		PlayerSingleton<PlayerCamera>.Instance.SetCanLook(c: false);
		PlayerSingleton<PlayerMovement>.Instance.canMove = false;
		Singleton<HUD>.Instance.SetCrosshairVisible(vis: false);
		Singleton<HUD>.Instance.canvas.enabled = false;
	}

	public void SetVisibleToLocalPlayer(bool vis)
	{
		avatarVisibleToLocalPlayer = vis;
		if (vis)
		{
			LayerUtility.SetLayerRecursively(Avatar.gameObject, LayerMask.NameToLayer("Player"));
		}
		else
		{
			LayerUtility.SetLayerRecursively(Avatar.gameObject, LayerMask.NameToLayer("Invisible"));
		}
	}

	[ObserversRpc(RunLocally = true)]
	public void SetPlayerCode(string code)
	{
		RpcWriter___Observers_SetPlayerCode_3615296227(code);
		RpcLogic___SetPlayerCode_3615296227(code);
	}

	[ServerRpc]
	public void SendPunch()
	{
		RpcWriter___Server_SendPunch_2166136261();
	}

	[ObserversRpc]
	private void Punch()
	{
		RpcWriter___Observers_Punch_2166136261();
	}

	[ServerRpc(RunLocally = true)]
	private void MarkIntroCompleted(BasicAvatarSettings appearance)
	{
		RpcWriter___Server_MarkIntroCompleted_3281254764(appearance);
		RpcLogic___MarkIntroCompleted_3281254764(appearance);
	}

	public bool IsPointVisibleToPlayer(Vector3 point, float maxDistance_Visible = 30f, float minDistance_Invisible = 5f)
	{
		float num = Vector3.Distance(point, MimicCamera.transform.position);
		if (num > maxDistance_Visible)
		{
			return false;
		}
		if (num < minDistance_Invisible)
		{
			return true;
		}
		if (MimicCamera.InverseTransformPoint(point).z < 0f)
		{
			return false;
		}
		if (Physics.Raycast(MimicCamera.transform.position, (point - MimicCamera.transform.position).normalized, out var _, Mathf.Min(maxDistance_Visible, num - 0.5f), 1 << LayerMask.NameToLayer("Default")))
		{
			return false;
		}
		return true;
	}

	public static Player GetClosestPlayer(Vector3 point, out float distance, List<Player> exclude = null)
	{
		distance = 0f;
		List<Player> list = new List<Player>();
		list.AddRange(PlayerList);
		if (exclude != null)
		{
			list = list.Except(exclude).ToList();
		}
		Player player = list.OrderBy((Player x) => Vector3.Distance(point, x.Avatar.CenterPoint)).FirstOrDefault();
		if (player != null)
		{
			distance = Vector3.Distance(point, player.Avatar.CenterPoint);
			return player;
		}
		return null;
	}

	public void SetCapsuleColliderHeight(float normalizedHeight)
	{
		CapCol.height = 2f * normalizedHeight;
		CapCol.center = new Vector3(0f, (0f - (2f - CapCol.height)) / 2f, 0f);
	}

	public virtual string GetSaveString()
	{
		return GetPlayerData().GetJson();
	}

	public PlayerData GetPlayerData()
	{
		return new PlayerData(SyncAccessor__003CPlayerCode_003Ek__BackingField, base.transform.position, base.transform.eulerAngles.y, HasCompletedIntro);
	}

	public virtual List<string> WriteData(string parentFolderPath)
	{
		List<string> result = new List<string>();
		((ISaveable)this).WriteSubfile(parentFolderPath, "Inventory", GetInventoryString());
		if (CurrentAvatarSettings != null)
		{
			string appearanceString = GetAppearanceString();
			((ISaveable)this).WriteSubfile(parentFolderPath, "Appearance", appearanceString);
		}
		string path = ((ISaveable)this).WriteFolder(parentFolderPath, "Variables");
		for (int i = 0; i < PlayerVariables.Count; i++)
		{
			if (PlayerVariables[i] != null && PlayerVariables[i].Persistent)
			{
				string json = new VariableData(PlayerVariables[i].Name, PlayerVariables[i].GetValue().ToString()).GetJson();
				string path2 = SaveManager.MakeFileSafe(PlayerVariables[i].Name) + ".json";
				string text = Path.Combine(path, path2);
				try
				{
					File.WriteAllText(text, json);
				}
				catch (Exception ex)
				{
					Console.LogWarning("Failed to write player variable file: " + text + " - " + ex.Message);
				}
			}
		}
		return result;
	}

	public string GetInventoryString()
	{
		return new ItemSet(Inventory.ToList()).GetJSON();
	}

	public string GetAppearanceString()
	{
		if (CurrentAvatarSettings != null)
		{
			return CurrentAvatarSettings.GetJson();
		}
		return string.Empty;
	}

	public string GetClothingString()
	{
		return new ItemSet(Clothing.ItemSlots.ToList()).GetJSON();
	}

	public virtual void Load(PlayerData data, string containerPath)
	{
		Load(data);
		if (Loader.TryLoadFile(containerPath, "Inventory", out var contents))
		{
			LoadInventory(contents);
		}
		else
		{
			Console.LogWarning("Failed to load player inventory under " + containerPath);
		}
		if (Loader.TryLoadFile(containerPath, "Appearance", out var contents2))
		{
			LoadAppearance(contents2);
		}
		else
		{
			Console.LogWarning("Failed to load player appearance under " + containerPath);
		}
		string path = Path.Combine(containerPath, "Variables");
		if (!Directory.Exists(path))
		{
			return;
		}
		string[] files = Directory.GetFiles(path);
		PlayerLoader playerLoader = new PlayerLoader();
		for (int i = 0; i < files.Length; i++)
		{
			if (playerLoader.TryLoadFile(files[i], out var contents3, autoAddExtension: false))
			{
				VariableData data2 = null;
				try
				{
					data2 = JsonUtility.FromJson<VariableData>(contents3);
				}
				catch (Exception ex)
				{
					Debug.LogError("Error loading variable data: " + ex.Message);
				}
				if (data != null)
				{
					LoadVariable(data2);
				}
			}
		}
	}

	public virtual void Load(PlayerData data)
	{
		playerDataRetrieveReturned = true;
		if (base.IsOwner)
		{
			PlayerSingleton<PlayerMovement>.Instance.Teleport(data.Position);
			base.transform.eulerAngles = new Vector3(0f, data.Rotation, 0f);
		}
		HasCompletedIntro = data.IntroCompleted;
	}

	public virtual void LoadInventory(string contentsString)
	{
		if (string.IsNullOrEmpty(contentsString))
		{
			Console.LogWarning("Empty inventory string");
		}
		else
		{
			if (!base.IsOwner)
			{
				return;
			}
			ItemInstance[] array = ItemSet.Deserialize(contentsString);
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] is CashInstance)
				{
					PlayerSingleton<PlayerInventory>.Instance.cashInstance.SetBalance((array[i] as CashInstance).Balance);
				}
				else if (i < 8)
				{
					PlayerSingleton<PlayerInventory>.Instance.hotbarSlots[i].SetStoredItem(array[i]);
				}
				else
				{
					Console.LogWarning("Hotbar slot out of range");
				}
			}
		}
	}

	public virtual void LoadAppearance(string appearanceString)
	{
		if (string.IsNullOrEmpty(appearanceString))
		{
			Console.LogWarning("Empty appearance string");
			return;
		}
		BasicAvatarSettings basicAvatarSettings = ScriptableObject.CreateInstance<BasicAvatarSettings>();
		JsonUtility.FromJsonOverwrite(appearanceString, basicAvatarSettings);
		SetAppearance(basicAvatarSettings);
	}

	public virtual void LoadClothing(string contentsString)
	{
		if (string.IsNullOrEmpty(contentsString))
		{
			Console.LogWarning("Empty clothing string");
		}
		else
		{
			if (!base.IsOwner)
			{
				return;
			}
			ItemInstance[] array = ItemSet.Deserialize(contentsString);
			for (int i = 0; i < array.Length; i++)
			{
				if (i < Clothing.ItemSlots.Count)
				{
					Clothing.ItemSlots[i].SetStoredItem(array[i]);
				}
				else
				{
					Console.LogWarning("Clothing slot out of range");
				}
			}
		}
	}

	public void SetRagdolled(bool ragdolled)
	{
		if (ragdolled == IsRagdolled)
		{
			return;
		}
		IsRagdolled = ragdolled;
		Avatar.SetRagdollPhysicsEnabled(ragdolled, playStandUpAnim: false);
		Avatar.transform.localEulerAngles = Vector3.zero;
		if (base.IsOwner)
		{
			if (IsRagdolled)
			{
				LayerUtility.SetLayerRecursively(Avatar.gameObject, LayerMask.NameToLayer("Player"));
			}
			else
			{
				LayerUtility.SetLayerRecursively(Avatar.gameObject, LayerMask.NameToLayer("Invisible"));
			}
		}
		if (IsRagdolled)
		{
			if (onRagdoll != null)
			{
				onRagdoll.Invoke();
			}
		}
		else if (onRagdollEnd != null)
		{
			onRagdollEnd.Invoke();
		}
	}

	[ServerRpc(RequireOwnership = false, RunLocally = true)]
	public virtual void SendImpact(Impact impact)
	{
		RpcWriter___Server_SendImpact_427288424(impact);
		RpcLogic___SendImpact_427288424(impact);
	}

	[ObserversRpc(RunLocally = true)]
	public virtual void ReceiveImpact(Impact impact)
	{
		RpcWriter___Observers_ReceiveImpact_427288424(impact);
		RpcLogic___ReceiveImpact_427288424(impact);
	}

	public virtual void ProcessImpactForce(Vector3 forcePoint, Vector3 forceDirection, float force)
	{
		if (force >= 50f)
		{
			Avatar.Anim.Flinch(forceDirection, AvatarAnimation.EFlinchType.Light);
		}
	}

	public virtual void OnDied()
	{
		if (base.Owner.IsLocalClient)
		{
			PlayerSingleton<PlayerInventory>.Instance.SetInventoryEnabled(enabled: false);
			Singleton<HUD>.Instance.canvas.enabled = false;
			ExitAll();
			PlayerSingleton<PlayerCamera>.Instance.OverrideTransform(PlayerSingleton<PlayerCamera>.Instance.transform.position, PlayerSingleton<PlayerCamera>.Instance.transform.rotation, 0f);
			PlayerSingleton<PlayerCamera>.Instance.AddActiveUIElement("Dead");
		}
		ClearProduct();
		NameLabel.gameObject.SetActive(value: false);
		CapCol.enabled = false;
		SetRagdolled(ragdolled: true);
		Avatar.MiddleSpineRB.AddForce(base.transform.forward * 30f, ForceMode.VelocityChange);
		Avatar.MiddleSpineRB.AddRelativeTorque(new Vector3(0f, UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 10f, ForceMode.VelocityChange);
		if (CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None)
		{
			IsArrested = true;
		}
		if (base.Owner.IsLocalClient)
		{
			Singleton<DeathScreen>.Instance.Open();
		}
	}

	public virtual void OnRevived()
	{
		SetRagdolled(ragdolled: false);
		if (!base.Owner.IsLocalClient)
		{
			NameLabel.gameObject.SetActive(value: true);
		}
		PlayerSingleton<PlayerCamera>.Instance.StopTransformOverride(0f, reenableCameraLook: false, returnToOriginalRotation: false);
		PlayerSingleton<PlayerCamera>.Instance.RemoveActiveUIElement("Dead");
		CapCol.enabled = true;
	}

	[ObserversRpc(RunLocally = true)]
	public void Arrest()
	{
		RpcWriter___Observers_Arrest_2166136261();
		RpcLogic___Arrest_2166136261();
	}

	public void Free()
	{
		if (IsArrested)
		{
			if (base.IsOwner)
			{
				Transform spawnPoint = Singleton<ScheduleOne.Map.Map>.Instance.PoliceStation.SpawnPoint;
				spawnPoint = NetworkSingleton<GameManager>.Instance.NoHomeRespawnPoint;
				_ = NetworkSingleton<CurfewManager>.Instance.IsCurrentlyActive;
				spawnPoint = ((Local.LastVisitedProperty != null) ? Local.LastVisitedProperty.InteriorSpawnPoint : ((ScheduleOne.Property.Property.OwnedProperties.Count <= 0) ? ScheduleOne.Property.Property.UnownedProperties[0].InteriorSpawnPoint : ScheduleOne.Property.Property.OwnedProperties[0].InteriorSpawnPoint));
				PlayerSingleton<PlayerMovement>.Instance.Teleport(spawnPoint.position + Vector3.up * 1f);
				base.transform.forward = spawnPoint.forward;
				Singleton<HUD>.Instance.canvas.enabled = true;
			}
			IsArrested = false;
			PlayerSingleton<PlayerCamera>.Instance.RemoveActiveUIElement("Arrested");
			if (onFreed != null)
			{
				onFreed.Invoke();
			}
		}
	}

	[ServerRpc(RunLocally = true)]
	public void SendPassOut()
	{
		RpcWriter___Server_SendPassOut_2166136261();
		RpcLogic___SendPassOut_2166136261();
	}

	[ObserversRpc(RunLocally = true, ExcludeOwner = true)]
	public void PassOut()
	{
		RpcWriter___Observers_PassOut_2166136261();
		RpcLogic___PassOut_2166136261();
	}

	[ServerRpc(RunLocally = true)]
	public void SendPassOutRecovery()
	{
		RpcWriter___Server_SendPassOutRecovery_2166136261();
		RpcLogic___SendPassOutRecovery_2166136261();
	}

	[ObserversRpc(RunLocally = true, ExcludeOwner = true)]
	public void PassOutRecovery()
	{
		RpcWriter___Observers_PassOutRecovery_2166136261();
		RpcLogic___PassOutRecovery_2166136261();
	}

	[ServerRpc(RunLocally = true, RequireOwnership = false)]
	public void SendEquippable_Networked(string assetPath)
	{
		RpcWriter___Server_SendEquippable_Networked_3615296227(assetPath);
		RpcLogic___SendEquippable_Networked_3615296227(assetPath);
	}

	[ObserversRpc(RunLocally = true)]
	private void SetEquippable_Networked(string assetPath)
	{
		RpcWriter___Observers_SetEquippable_Networked_3615296227(assetPath);
		RpcLogic___SetEquippable_Networked_3615296227(assetPath);
	}

	[ServerRpc(RunLocally = true)]
	public void SendEquippableMessage_Networked(string message, object data)
	{
		RpcWriter___Server_SendEquippableMessage_Networked_449655107(message, data);
		RpcLogic___SendEquippableMessage_Networked_449655107(message, data);
	}

	[ObserversRpc(RunLocally = true)]
	public void SendEquippableMessage_Networked_Vector(string message, Vector3 data)
	{
		RpcWriter___Observers_SendEquippableMessage_Networked_Vector_1512140110(message, data);
		RpcLogic___SendEquippableMessage_Networked_Vector_1512140110(message, data);
	}

	[ServerRpc(RunLocally = true)]
	public void SendAnimationTrigger(string trigger)
	{
		RpcWriter___Server_SendAnimationTrigger_3615296227(trigger);
		RpcLogic___SendAnimationTrigger_3615296227(trigger);
	}

	[ObserversRpc(RunLocally = true)]
	[TargetRpc]
	public void SetAnimationTrigger_Networked(NetworkConnection conn, string trigger)
	{
		if ((object)conn == null)
		{
			RpcWriter___Observers_SetAnimationTrigger_Networked_2971853958(conn, trigger);
			RpcLogic___SetAnimationTrigger_Networked_2971853958(conn, trigger);
		}
		else
		{
			RpcWriter___Target_SetAnimationTrigger_Networked_2971853958(conn, trigger);
		}
	}

	public void SetAnimationTrigger(string trigger)
	{
		Avatar.Anim.SetTrigger(trigger);
	}

	[ObserversRpc(RunLocally = true)]
	[TargetRpc]
	public void ResetAnimationTrigger_Networked(NetworkConnection conn, string trigger)
	{
		if ((object)conn == null)
		{
			RpcWriter___Observers_ResetAnimationTrigger_Networked_2971853958(conn, trigger);
			RpcLogic___ResetAnimationTrigger_Networked_2971853958(conn, trigger);
		}
		else
		{
			RpcWriter___Target_ResetAnimationTrigger_Networked_2971853958(conn, trigger);
		}
	}

	public void ResetAnimationTrigger(string trigger)
	{
		Avatar.Anim.ResetTrigger(trigger);
	}

	[ServerRpc(RunLocally = true)]
	public void SendAnimationBool(string name, bool val)
	{
		RpcWriter___Server_SendAnimationBool_310431262(name, val);
		RpcLogic___SendAnimationBool_310431262(name, val);
	}

	[ObserversRpc(RunLocally = true)]
	public void SetAnimationBool(string name, bool val)
	{
		RpcWriter___Observers_SetAnimationBool_310431262(name, val);
		RpcLogic___SetAnimationBool_310431262(name, val);
	}

	[ObserversRpc]
	public void Taze()
	{
		RpcWriter___Observers_Taze_2166136261();
	}

	[ServerRpc(RunLocally = true)]
	public void SetInventoryItem(int index, ItemInstance item)
	{
		RpcWriter___Server_SetInventoryItem_2317364410(index, item);
		RpcLogic___SetInventoryItem_2317364410(index, item);
	}

	private void GetNetworth(MoneyManager.FloatContainer container)
	{
		for (int i = 0; i < Inventory.Length; i++)
		{
			if (Inventory[i].ItemInstance != null)
			{
				container.ChangeValue(Inventory[i].ItemInstance.GetMonetaryValue());
			}
		}
	}

	[ServerRpc(RunLocally = true)]
	public void SendAppearance(BasicAvatarSettings settings)
	{
		RpcWriter___Server_SendAppearance_3281254764(settings);
		RpcLogic___SendAppearance_3281254764(settings);
	}

	[ObserversRpc(RunLocally = true)]
	public void SetAppearance(BasicAvatarSettings settings)
	{
		RpcWriter___Observers_SetAppearance_3281254764(settings);
		RpcLogic___SetAppearance_3281254764(settings);
	}

	public void MountSkateboard(Skateboard board)
	{
		SendMountedSkateboard(board.NetworkObject);
		Collider[] componentsInChildren = GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren)
		{
			Collider[] mainColliders = board.MainColliders;
			foreach (Collider collider2 in mainColliders)
			{
				Physics.IgnoreCollision(collider, collider2, ignore: true);
			}
		}
		PlayerSingleton<PlayerCamera>.Instance.OverrideTransform(PlayerSingleton<PlayerCamera>.Instance.transform.position, PlayerSingleton<PlayerCamera>.Instance.transform.rotation, 0f);
		PlayerSingleton<PlayerCamera>.Instance.SetCanLook(c: false);
		PlayerSingleton<PlayerCamera>.Instance.SetCameraMode(PlayerCamera.ECameraMode.Skateboard);
		PlayerSingleton<PlayerCamera>.Instance.transform.position = PlayerSingleton<PlayerCamera>.Instance.transform.transform.position - base.transform.forward * 0.5f;
		SetVisibleToLocalPlayer(vis: true);
		CapCol.enabled = true;
		PlayerSingleton<PlayerMovement>.Instance.canMove = false;
		PlayerSingleton<PlayerMovement>.Instance.Controller.enabled = false;
		Singleton<HUD>.Instance.SetCrosshairVisible(vis: false);
		Singleton<InputPromptsCanvas>.Instance.LoadModule("skateboard");
	}

	[ServerRpc(RunLocally = true)]
	private void SendMountedSkateboard(NetworkObject skateboardObj)
	{
		RpcWriter___Server_SendMountedSkateboard_3323014238(skateboardObj);
		RpcLogic___SendMountedSkateboard_3323014238(skateboardObj);
	}

	[ObserversRpc(RunLocally = true)]
	private void SetMountedSkateboard(NetworkObject skateboardObj)
	{
		RpcWriter___Observers_SetMountedSkateboard_3323014238(skateboardObj);
		RpcLogic___SetMountedSkateboard_3323014238(skateboardObj);
	}

	public void DismountSkateboard()
	{
		SendMountedSkateboard(null);
		SetVisibleToLocalPlayer(vis: false);
		CapCol.enabled = true;
		SetCapsuleColliderHeight(1f);
		PlayerSingleton<PlayerMovement>.Instance.canMove = true;
		PlayerSingleton<PlayerMovement>.Instance.Controller.enabled = true;
		PlayerSingleton<PlayerCamera>.Instance.SetCanLook(c: true);
		PlayerSingleton<PlayerCamera>.Instance.StopTransformOverride(0f, reenableCameraLook: true, returnToOriginalRotation: false);
		PlayerSingleton<PlayerCamera>.Instance.StopFOVOverride(0f);
		PlayerSingleton<PlayerCamera>.Instance.SetCameraMode(PlayerCamera.ECameraMode.Default);
		Singleton<HUD>.Instance.SetCrosshairVisible(vis: true);
		Singleton<InputPromptsCanvas>.Instance.UnloadModule();
	}

	public void ConsumeProduct(ProductItemInstance product)
	{
		SendConsumeProduct(product);
		ConsumeProductInternal(product);
	}

	[ServerRpc(RequireOwnership = false)]
	private void SendConsumeProduct(ProductItemInstance product)
	{
		RpcWriter___Server_SendConsumeProduct_2622925554(product);
	}

	[ObserversRpc]
	private void ReceiveConsumeProduct(ProductItemInstance product)
	{
		RpcWriter___Observers_ReceiveConsumeProduct_2622925554(product);
	}

	private void ConsumeProductInternal(ProductItemInstance product)
	{
		if (ConsumedProduct != null)
		{
			ClearProduct();
		}
		ConsumedProduct = product;
		TimeSinceProductConsumed = 0;
		product.ApplyEffectsToPlayer(this);
	}

	public void ClearProduct()
	{
		if (ConsumedProduct != null)
		{
			ConsumedProduct.ClearEffectsFromPlayer(this);
			ConsumedProduct = null;
		}
	}

	private void CreatePlayerVariables()
	{
		if (VariableDict.Count <= 0)
		{
			Console.Log("Creating player variables for " + SyncAccessor__003CPlayerName_003Ek__BackingField + " (" + SyncAccessor__003CPlayerCode_003Ek__BackingField + ")");
			NetworkSingleton<VariableDatabase>.Instance.CreatePlayerVariables(this);
			if (InstanceFinder.IsServer)
			{
				SetVariableValue("IsServer", true.ToString());
			}
		}
	}

	public BaseVariable GetVariable(string variableName)
	{
		variableName = variableName.ToLower();
		if (VariableDict.ContainsKey(variableName))
		{
			return VariableDict[variableName];
		}
		Console.LogWarning("Failed to find variable with name: " + variableName);
		return null;
	}

	public T GetValue<T>(string variableName)
	{
		variableName = variableName.ToLower();
		if (VariableDict.ContainsKey(variableName))
		{
			return (T)VariableDict[variableName].GetValue();
		}
		Console.LogError("Variable with name " + variableName + " does not exist in the database.");
		return default(T);
	}

	public void SetVariableValue(string variableName, string value, bool network = true)
	{
		variableName = variableName.ToLower();
		if (VariableDict.ContainsKey(variableName))
		{
			VariableDict[variableName].SetValue(value, network);
		}
		else
		{
			Console.LogWarning("Failed to find variable with name: " + variableName);
		}
	}

	public void AddVariable(BaseVariable variable)
	{
		if (VariableDict.ContainsKey(variable.Name.ToLower()))
		{
			Console.LogError("Variable with name " + variable.Name + " already exists in the database.");
			return;
		}
		PlayerVariables.Add(variable);
		VariableDict.Add(variable.Name.ToLower(), variable);
	}

	[ServerRpc(RequireOwnership = false, RunLocally = true)]
	public void SendValue(string variableName, string value, bool sendToOwner)
	{
		RpcWriter___Server_SendValue_3589193952(variableName, value, sendToOwner);
		RpcLogic___SendValue_3589193952(variableName, value, sendToOwner);
	}

	[TargetRpc]
	private void ReceiveValue(NetworkConnection conn, string variableName, string value)
	{
		RpcWriter___Target_ReceiveValue_3895153758(conn, variableName, value);
	}

	private void ReceiveValue(string variableName, string value)
	{
		variableName = variableName.ToLower();
		if (VariableDict.ContainsKey(variableName))
		{
			VariableDict[variableName].SetValue(value, replicate: false);
		}
		else
		{
			Console.LogWarning("Failed to find player variable with name: " + variableName);
		}
	}

	public void LoadVariable(VariableData data)
	{
		BaseVariable variable = GetVariable(data.Name);
		if (variable == null)
		{
			Console.LogWarning("Failed to find variable with name: " + data.Name);
		}
		else
		{
			variable.SetValue(data.Value);
		}
	}

	public virtual void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerAssembly_002DCSharp_002Edll_Excuted = true;
			syncVar____003CCameraRotation_003Ek__BackingField = new SyncVar<Quaternion>(this, 6u, WritePermission.ServerOnly, ReadPermission.Observers, 0.1f, Channel.Unreliable, CameraRotation);
			syncVar____003CCameraPosition_003Ek__BackingField = new SyncVar<Vector3>(this, 5u, WritePermission.ServerOnly, ReadPermission.Observers, 0.1f, Channel.Unreliable, CameraPosition);
			syncVar____003CIsReadyToSleep_003Ek__BackingField = new SyncVar<bool>(this, 4u, WritePermission.ServerOnly, ReadPermission.Observers, -1f, Channel.Reliable, IsReadyToSleep);
			syncVar____003CCurrentBed_003Ek__BackingField = new SyncVar<NetworkObject>(this, 3u, WritePermission.ServerOnly, ReadPermission.Observers, -1f, Channel.Reliable, CurrentBed);
			syncVar____003CCurrentVehicle_003Ek__BackingField = new SyncVar<NetworkObject>(this, 2u, WritePermission.ServerOnly, ReadPermission.Observers, -1f, Channel.Reliable, CurrentVehicle);
			syncVar____003CCurrentVehicle_003Ek__BackingField.OnChange += CurrentVehicleChanged;
			syncVar____003CPlayerCode_003Ek__BackingField = new SyncVar<string>(this, 1u, WritePermission.ClientUnsynchronized, ReadPermission.Observers, -1f, Channel.Reliable, PlayerCode);
			syncVar____003CPlayerName_003Ek__BackingField = new SyncVar<string>(this, 0u, WritePermission.ClientUnsynchronized, ReadPermission.Observers, -1f, Channel.Reliable, PlayerName);
			RegisterServerRpc(0u, RpcReader___Server_set_CurrentVehicle_3323014238);
			RegisterServerRpc(1u, RpcReader___Server_set_CurrentBed_3323014238);
			RegisterServerRpc(2u, RpcReader___Server_set_IsSkating_1140765316);
			RegisterServerRpc(3u, RpcReader___Server_set_CameraPosition_4276783012);
			RegisterServerRpc(4u, RpcReader___Server_set_CameraRotation_3429297120);
			RegisterServerRpc(5u, RpcReader___Server_RequestSavePlayer_2166136261);
			RegisterObserversRpc(6u, RpcReader___Observers_ReturnSaveRequest_214505783);
			RegisterTargetRpc(7u, RpcReader___Target_ReturnSaveRequest_214505783);
			RegisterObserversRpc(8u, RpcReader___Observers_HostExitedGame_2166136261);
			RegisterServerRpc(9u, RpcReader___Server_SendPlayerNameData_586648380);
			RegisterServerRpc(10u, RpcReader___Server_RequestPlayerData_3615296227);
			RegisterServerRpc(11u, RpcReader___Server_MarkPlayerInitialized_2166136261);
			RegisterObserversRpc(12u, RpcReader___Observers_ReceivePlayerData_3244732873);
			RegisterTargetRpc(13u, RpcReader___Target_ReceivePlayerData_3244732873);
			RegisterObserversRpc(14u, RpcReader___Observers_ReceivePlayerNameData_3895153758);
			RegisterTargetRpc(15u, RpcReader___Target_ReceivePlayerNameData_3895153758);
			RegisterServerRpc(16u, RpcReader___Server_SendFlashlightOnNetworked_1140765316);
			RegisterObserversRpc(17u, RpcReader___Observers_SetFlashlightOn_1140765316);
			RegisterObserversRpc(18u, RpcReader___Observers_PlayJumpAnimation_2166136261);
			RegisterServerRpc(19u, RpcReader___Server_SendCrouched_1140765316);
			RegisterTargetRpc(20u, RpcReader___Target_ReceiveCrouched_214505783);
			RegisterObserversRpc(21u, RpcReader___Observers_ReceiveCrouched_214505783);
			RegisterServerRpc(22u, RpcReader___Server_SendAvatarSettings_4281687581);
			RegisterObserversRpc(23u, RpcReader___Observers_SetAvatarSettings_4281687581);
			RegisterObserversRpc(24u, RpcReader___Observers_SetVisible_Networked_1140765316);
			RegisterServerRpc(25u, RpcReader___Server_SetReadyToSleep_1140765316);
			RegisterObserversRpc(26u, RpcReader___Observers_SetPlayerCode_3615296227);
			RegisterServerRpc(27u, RpcReader___Server_SendPunch_2166136261);
			RegisterObserversRpc(28u, RpcReader___Observers_Punch_2166136261);
			RegisterServerRpc(29u, RpcReader___Server_MarkIntroCompleted_3281254764);
			RegisterServerRpc(30u, RpcReader___Server_SendImpact_427288424);
			RegisterObserversRpc(31u, RpcReader___Observers_ReceiveImpact_427288424);
			RegisterObserversRpc(32u, RpcReader___Observers_Arrest_2166136261);
			RegisterServerRpc(33u, RpcReader___Server_SendPassOut_2166136261);
			RegisterObserversRpc(34u, RpcReader___Observers_PassOut_2166136261);
			RegisterServerRpc(35u, RpcReader___Server_SendPassOutRecovery_2166136261);
			RegisterObserversRpc(36u, RpcReader___Observers_PassOutRecovery_2166136261);
			RegisterServerRpc(37u, RpcReader___Server_SendEquippable_Networked_3615296227);
			RegisterObserversRpc(38u, RpcReader___Observers_SetEquippable_Networked_3615296227);
			RegisterServerRpc(39u, RpcReader___Server_SendEquippableMessage_Networked_449655107);
			RegisterObserversRpc(40u, RpcReader___Observers_SendEquippableMessage_Networked_Vector_1512140110);
			RegisterServerRpc(41u, RpcReader___Server_SendAnimationTrigger_3615296227);
			RegisterObserversRpc(42u, RpcReader___Observers_SetAnimationTrigger_Networked_2971853958);
			RegisterTargetRpc(43u, RpcReader___Target_SetAnimationTrigger_Networked_2971853958);
			RegisterObserversRpc(44u, RpcReader___Observers_ResetAnimationTrigger_Networked_2971853958);
			RegisterTargetRpc(45u, RpcReader___Target_ResetAnimationTrigger_Networked_2971853958);
			RegisterServerRpc(46u, RpcReader___Server_SendAnimationBool_310431262);
			RegisterObserversRpc(47u, RpcReader___Observers_SetAnimationBool_310431262);
			RegisterObserversRpc(48u, RpcReader___Observers_Taze_2166136261);
			RegisterServerRpc(49u, RpcReader___Server_SetInventoryItem_2317364410);
			RegisterServerRpc(50u, RpcReader___Server_SendAppearance_3281254764);
			RegisterObserversRpc(51u, RpcReader___Observers_SetAppearance_3281254764);
			RegisterServerRpc(52u, RpcReader___Server_SendMountedSkateboard_3323014238);
			RegisterObserversRpc(53u, RpcReader___Observers_SetMountedSkateboard_3323014238);
			RegisterServerRpc(54u, RpcReader___Server_SendConsumeProduct_2622925554);
			RegisterObserversRpc(55u, RpcReader___Observers_ReceiveConsumeProduct_2622925554);
			RegisterServerRpc(56u, RpcReader___Server_SendValue_3589193952);
			RegisterTargetRpc(57u, RpcReader___Target_ReceiveValue_3895153758);
			RegisterSyncVarRead(ReadSyncVar___ScheduleOne_002EPlayerScripts_002EPlayer);
		}
	}

	public virtual void NetworkInitialize__Late()
	{
		if (!NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerAssembly_002DCSharp_002Edll_Excuted = true;
			syncVar____003CCameraRotation_003Ek__BackingField.SetRegistered();
			syncVar____003CCameraPosition_003Ek__BackingField.SetRegistered();
			syncVar____003CIsReadyToSleep_003Ek__BackingField.SetRegistered();
			syncVar____003CCurrentBed_003Ek__BackingField.SetRegistered();
			syncVar____003CCurrentVehicle_003Ek__BackingField.SetRegistered();
			syncVar____003CPlayerCode_003Ek__BackingField.SetRegistered();
			syncVar____003CPlayerName_003Ek__BackingField.SetRegistered();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize__Late();
	}

	private void RpcWriter___Server_set_CurrentVehicle_3323014238(NetworkObject value)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteNetworkObject(value);
			SendServerRpc(0u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	[SpecialName]
	public void RpcLogic___set_CurrentVehicle_3323014238(NetworkObject value)
	{
		this.sync___set_value__003CCurrentVehicle_003Ek__BackingField(value, asServer: true);
	}

	private void RpcReader___Server_set_CurrentVehicle_3323014238(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		NetworkObject value = PooledReader0.ReadNetworkObject();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___set_CurrentVehicle_3323014238(value);
		}
	}

	private void RpcWriter___Server_set_CurrentBed_3323014238(NetworkObject value)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteNetworkObject(value);
			SendServerRpc(1u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	[SpecialName]
	public void RpcLogic___set_CurrentBed_3323014238(NetworkObject value)
	{
		this.sync___set_value__003CCurrentBed_003Ek__BackingField(value, asServer: true);
	}

	private void RpcReader___Server_set_CurrentBed_3323014238(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		NetworkObject value = PooledReader0.ReadNetworkObject();
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___set_CurrentBed_3323014238(value);
		}
	}

	private void RpcWriter___Server_set_IsSkating_1140765316(bool value)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(value);
			SendServerRpc(2u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	[SpecialName]
	public void RpcLogic___set_IsSkating_1140765316(bool value)
	{
		IsSkating = value;
	}

	private void RpcReader___Server_set_IsSkating_1140765316(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		bool value = PooledReader0.ReadBoolean();
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___set_IsSkating_1140765316(value);
		}
	}

	private void RpcWriter___Server_set_CameraPosition_4276783012(Vector3 value)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteVector3(value);
			SendServerRpc(3u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	[SpecialName]
	public void RpcLogic___set_CameraPosition_4276783012(Vector3 value)
	{
		this.sync___set_value__003CCameraPosition_003Ek__BackingField(value, asServer: true);
	}

	private void RpcReader___Server_set_CameraPosition_4276783012(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		Vector3 value = PooledReader0.ReadVector3();
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___set_CameraPosition_4276783012(value);
		}
	}

	private void RpcWriter___Server_set_CameraRotation_3429297120(Quaternion value)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteQuaternion(value);
			SendServerRpc(4u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	[SpecialName]
	public void RpcLogic___set_CameraRotation_3429297120(Quaternion value)
	{
		this.sync___set_value__003CCameraRotation_003Ek__BackingField(value, asServer: true);
	}

	private void RpcReader___Server_set_CameraRotation_3429297120(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		Quaternion value = PooledReader0.ReadQuaternion();
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___set_CameraRotation_3429297120(value);
		}
	}

	private void RpcWriter___Server_RequestSavePlayer_2166136261()
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendServerRpc(5u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___RequestSavePlayer_2166136261()
	{
		playerSaveRequestReturned = false;
		if (InstanceFinder.IsServer)
		{
			Console.Log("Save request received");
			Singleton<PlayerManager>.Instance.SavePlayer(this);
			ReturnSaveRequest(base.Owner, successful: true);
		}
	}

	private void RpcReader___Server_RequestSavePlayer_2166136261(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		if (base.IsServerInitialized && !conn.IsLocalClient)
		{
			RpcLogic___RequestSavePlayer_2166136261();
		}
	}

	private void RpcWriter___Observers_ReturnSaveRequest_214505783(NetworkConnection conn, bool successful)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(successful);
			SendObserversRpc(6u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___ReturnSaveRequest_214505783(NetworkConnection conn, bool successful)
	{
		Console.Log("Save request returned. Successful: " + successful);
		playerSaveRequestReturned = true;
	}

	private void RpcReader___Observers_ReturnSaveRequest_214505783(PooledReader PooledReader0, Channel channel)
	{
		bool successful = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized)
		{
			RpcLogic___ReturnSaveRequest_214505783(null, successful);
		}
	}

	private void RpcWriter___Target_ReturnSaveRequest_214505783(NetworkConnection conn, bool successful)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(successful);
			SendTargetRpc(7u, writer, channel, DataOrderType.Default, conn, excludeServer: false);
			writer.Store();
		}
	}

	private void RpcReader___Target_ReturnSaveRequest_214505783(PooledReader PooledReader0, Channel channel)
	{
		bool successful = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized)
		{
			RpcLogic___ReturnSaveRequest_214505783(base.LocalConnection, successful);
		}
	}

	private void RpcWriter___Observers_HostExitedGame_2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendObserversRpc(8u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___HostExitedGame_2166136261()
	{
		if (!InstanceFinder.IsServer && (!Singleton<LoadManager>.InstanceExists || (!Singleton<LoadManager>.Instance.IsLoading && Singleton<LoadManager>.Instance.IsGameLoaded)))
		{
			Console.Log("Host exited game");
			Singleton<LoadManager>.Instance.ExitToMenu(null, new MainMenuPopup.Data("Exited Game", "Host left the game", isBad: false));
		}
	}

	private void RpcReader___Observers_HostExitedGame_2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___HostExitedGame_2166136261();
		}
	}

	private void RpcWriter___Server_SendPlayerNameData_586648380(string playerName, ulong id)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(playerName);
			writer.WriteUInt64(id);
			SendServerRpc(9u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendPlayerNameData_586648380(string playerName, ulong id)
	{
		ReceivePlayerNameData(null, playerName, id.ToString());
		PlayerName = playerName;
		PlayerCode = id.ToString();
	}

	private void RpcReader___Server_SendPlayerNameData_586648380(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string playerName = PooledReader0.ReadString();
		ulong id = PooledReader0.ReadUInt64();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendPlayerNameData_586648380(playerName, id);
		}
	}

	private void RpcWriter___Server_RequestPlayerData_3615296227(string playerCode)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(playerCode);
			SendServerRpc(10u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___RequestPlayerData_3615296227(string playerCode)
	{
		Singleton<PlayerManager>.Instance.TryGetPlayerData(playerCode, out var data, out var inventoryString, out var appearanceString, out var clothingString, out var variables);
		Console.Log("Sending player data for " + playerCode + " (" + data?.ToString() + ")");
		ReceivePlayerData(null, data, inventoryString, appearanceString, clothingString, variables);
	}

	private void RpcReader___Server_RequestPlayerData_3615296227(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string playerCode = PooledReader0.ReadString();
		if (base.IsServerInitialized)
		{
			RpcLogic___RequestPlayerData_3615296227(playerCode);
		}
	}

	private void RpcWriter___Server_MarkPlayerInitialized_2166136261()
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendServerRpc(11u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___MarkPlayerInitialized_2166136261()
	{
		Console.Log(SyncAccessor__003CPlayerName_003Ek__BackingField + " initialized over network");
		PlayerInitializedOverNetwork = true;
	}

	private void RpcReader___Server_MarkPlayerInitialized_2166136261(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___MarkPlayerInitialized_2166136261();
		}
	}

	private void RpcWriter___Observers_ReceivePlayerData_3244732873(NetworkConnection conn, PlayerData data, string inventoryString, string appearanceString, string clothigString, VariableData[] vars)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002EPersistence_002EDatas_002EPlayerDataFishNet_002ESerializing_002EGenerated(writer, data);
			writer.WriteString(inventoryString);
			writer.WriteString(appearanceString);
			writer.WriteString(clothigString);
			GeneratedWriters___Internal.Write___ScheduleOne_002EPersistence_002EDatas_002EVariableData_005B_005DFishNet_002ESerializing_002EGenerated(writer, vars);
			SendObserversRpc(12u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___ReceivePlayerData_3244732873(NetworkConnection conn, PlayerData data, string inventoryString, string appearanceString, string clothigString, VariableData[] vars)
	{
		playerDataRetrieveReturned = true;
		if (data != null)
		{
			Load(data);
			if (!string.IsNullOrEmpty(inventoryString))
			{
				LoadInventory(inventoryString);
			}
			if (!string.IsNullOrEmpty(appearanceString))
			{
				LoadAppearance(appearanceString);
			}
		}
		else if (base.IsOwner)
		{
			Console.Log("No player data found for this player; first time joining");
		}
		if (!base.IsOwner)
		{
			return;
		}
		if (vars != null)
		{
			foreach (VariableData data2 in vars)
			{
				LoadVariable(data2);
			}
		}
		PlayerLoaded();
	}

	private void RpcReader___Observers_ReceivePlayerData_3244732873(PooledReader PooledReader0, Channel channel)
	{
		PlayerData data = GeneratedReaders___Internal.Read___ScheduleOne_002EPersistence_002EDatas_002EPlayerDataFishNet_002ESerializing_002EGenerateds(PooledReader0);
		string inventoryString = PooledReader0.ReadString();
		string appearanceString = PooledReader0.ReadString();
		string clothigString = PooledReader0.ReadString();
		VariableData[] vars = GeneratedReaders___Internal.Read___ScheduleOne_002EPersistence_002EDatas_002EVariableData_005B_005DFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___ReceivePlayerData_3244732873(null, data, inventoryString, appearanceString, clothigString, vars);
		}
	}

	private void RpcWriter___Target_ReceivePlayerData_3244732873(NetworkConnection conn, PlayerData data, string inventoryString, string appearanceString, string clothigString, VariableData[] vars)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002EPersistence_002EDatas_002EPlayerDataFishNet_002ESerializing_002EGenerated(writer, data);
			writer.WriteString(inventoryString);
			writer.WriteString(appearanceString);
			writer.WriteString(clothigString);
			GeneratedWriters___Internal.Write___ScheduleOne_002EPersistence_002EDatas_002EVariableData_005B_005DFishNet_002ESerializing_002EGenerated(writer, vars);
			SendTargetRpc(13u, writer, channel, DataOrderType.Default, conn, excludeServer: false);
			writer.Store();
		}
	}

	private void RpcReader___Target_ReceivePlayerData_3244732873(PooledReader PooledReader0, Channel channel)
	{
		PlayerData data = GeneratedReaders___Internal.Read___ScheduleOne_002EPersistence_002EDatas_002EPlayerDataFishNet_002ESerializing_002EGenerateds(PooledReader0);
		string inventoryString = PooledReader0.ReadString();
		string appearanceString = PooledReader0.ReadString();
		string clothigString = PooledReader0.ReadString();
		VariableData[] vars = GeneratedReaders___Internal.Read___ScheduleOne_002EPersistence_002EDatas_002EVariableData_005B_005DFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsClientInitialized)
		{
			RpcLogic___ReceivePlayerData_3244732873(base.LocalConnection, data, inventoryString, appearanceString, clothigString, vars);
		}
	}

	private void RpcWriter___Observers_ReceivePlayerNameData_3895153758(NetworkConnection conn, string playerName, string id)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(playerName);
			writer.WriteString(id);
			SendObserversRpc(14u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___ReceivePlayerNameData_3895153758(NetworkConnection conn, string playerName, string id)
	{
		PlayerName = playerName;
		PlayerCode = id;
		base.gameObject.name = SyncAccessor__003CPlayerName_003Ek__BackingField + " (" + id + ")";
		PoI.SetMainText(SyncAccessor__003CPlayerName_003Ek__BackingField);
		NameLabel.ShowText(SyncAccessor__003CPlayerName_003Ek__BackingField);
	}

	private void RpcReader___Observers_ReceivePlayerNameData_3895153758(PooledReader PooledReader0, Channel channel)
	{
		string playerName = PooledReader0.ReadString();
		string id = PooledReader0.ReadString();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___ReceivePlayerNameData_3895153758(null, playerName, id);
		}
	}

	private void RpcWriter___Target_ReceivePlayerNameData_3895153758(NetworkConnection conn, string playerName, string id)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(playerName);
			writer.WriteString(id);
			SendTargetRpc(15u, writer, channel, DataOrderType.Default, conn, excludeServer: false);
			writer.Store();
		}
	}

	private void RpcReader___Target_ReceivePlayerNameData_3895153758(PooledReader PooledReader0, Channel channel)
	{
		string playerName = PooledReader0.ReadString();
		string id = PooledReader0.ReadString();
		if (base.IsClientInitialized)
		{
			RpcLogic___ReceivePlayerNameData_3895153758(base.LocalConnection, playerName, id);
		}
	}

	private void RpcWriter___Server_SendFlashlightOnNetworked_1140765316(bool on)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(on);
			SendServerRpc(16u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	private void RpcLogic___SendFlashlightOnNetworked_1140765316(bool on)
	{
		SetFlashlightOn(on);
	}

	private void RpcReader___Server_SendFlashlightOnNetworked_1140765316(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		bool flag = PooledReader0.ReadBoolean();
		if (base.IsServerInitialized && !conn.IsLocalClient)
		{
			RpcLogic___SendFlashlightOnNetworked_1140765316(flag);
		}
	}

	private void RpcWriter___Observers_SetFlashlightOn_1140765316(bool on)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(on);
			SendObserversRpc(17u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___SetFlashlightOn_1140765316(bool on)
	{
		ThirdPersonFlashlight.gameObject.SetActive(on && !base.IsOwner);
	}

	private void RpcReader___Observers_SetFlashlightOn_1140765316(PooledReader PooledReader0, Channel channel)
	{
		bool flag = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetFlashlightOn_1140765316(flag);
		}
	}

	private void RpcWriter___Observers_PlayJumpAnimation_2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendObserversRpc(18u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___PlayJumpAnimation_2166136261()
	{
		Anim.Jump();
	}

	private void RpcReader___Observers_PlayJumpAnimation_2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized)
		{
			RpcLogic___PlayJumpAnimation_2166136261();
		}
	}

	private void RpcWriter___Server_SendCrouched_1140765316(bool crouched)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(crouched);
			SendServerRpc(19u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendCrouched_1140765316(bool crouched)
	{
		ReceiveCrouched(null, crouched);
	}

	private void RpcReader___Server_SendCrouched_1140765316(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		bool crouched = PooledReader0.ReadBoolean();
		if (base.IsServerInitialized && !conn.IsLocalClient)
		{
			RpcLogic___SendCrouched_1140765316(crouched);
		}
	}

	private void RpcWriter___Target_ReceiveCrouched_214505783(NetworkConnection conn, bool crouched)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(crouched);
			SendTargetRpc(20u, writer, channel, DataOrderType.Default, conn, excludeServer: false);
			writer.Store();
		}
	}

	private void RpcLogic___ReceiveCrouched_214505783(NetworkConnection conn, bool crouched)
	{
		if (!base.Owner.IsLocalClient)
		{
			Crouched = crouched;
		}
	}

	private void RpcReader___Target_ReceiveCrouched_214505783(PooledReader PooledReader0, Channel channel)
	{
		bool crouched = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized)
		{
			RpcLogic___ReceiveCrouched_214505783(base.LocalConnection, crouched);
		}
	}

	private void RpcWriter___Observers_ReceiveCrouched_214505783(NetworkConnection conn, bool crouched)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(crouched);
			SendObserversRpc(21u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcReader___Observers_ReceiveCrouched_214505783(PooledReader PooledReader0, Channel channel)
	{
		bool crouched = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___ReceiveCrouched_214505783(null, crouched);
		}
	}

	private void RpcWriter___Server_SendAvatarSettings_4281687581(AvatarSettings settings)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002EAvatarFramework_002EAvatarSettingsFishNet_002ESerializing_002EGenerated(writer, settings);
			SendServerRpc(22u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendAvatarSettings_4281687581(AvatarSettings settings)
	{
		SetAvatarSettings(settings);
	}

	private void RpcReader___Server_SendAvatarSettings_4281687581(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		AvatarSettings settings = GeneratedReaders___Internal.Read___ScheduleOne_002EAvatarFramework_002EAvatarSettingsFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendAvatarSettings_4281687581(settings);
		}
	}

	private void RpcWriter___Observers_SetAvatarSettings_4281687581(AvatarSettings settings)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002EAvatarFramework_002EAvatarSettingsFishNet_002ESerializing_002EGenerated(writer, settings);
			SendObserversRpc(23u, writer, channel, DataOrderType.Default, bufferLast: true, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___SetAvatarSettings_4281687581(AvatarSettings settings)
	{
		Avatar.LoadAvatarSettings(settings);
		Console.Log("Received avatar settings");
		if (base.IsOwner)
		{
			LayerUtility.SetLayerRecursively(Avatar.gameObject, LayerMask.NameToLayer("Invisible"));
		}
	}

	private void RpcReader___Observers_SetAvatarSettings_4281687581(PooledReader PooledReader0, Channel channel)
	{
		AvatarSettings settings = GeneratedReaders___Internal.Read___ScheduleOne_002EAvatarFramework_002EAvatarSettingsFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetAvatarSettings_4281687581(settings);
		}
	}

	private void RpcWriter___Observers_SetVisible_Networked_1140765316(bool vis)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(vis);
			SendObserversRpc(24u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___SetVisible_Networked_1140765316(bool vis)
	{
		Avatar.SetVisible(vis);
		CapCol.enabled = vis;
	}

	private void RpcReader___Observers_SetVisible_Networked_1140765316(PooledReader PooledReader0, Channel channel)
	{
		bool vis = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized)
		{
			RpcLogic___SetVisible_Networked_1140765316(vis);
		}
	}

	private void RpcWriter___Server_SetReadyToSleep_1140765316(bool ready)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteBoolean(ready);
			SendServerRpc(25u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SetReadyToSleep_1140765316(bool ready)
	{
		IsReadyToSleep = ready;
	}

	private void RpcReader___Server_SetReadyToSleep_1140765316(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		bool ready = PooledReader0.ReadBoolean();
		if (base.IsServerInitialized && !conn.IsLocalClient)
		{
			RpcLogic___SetReadyToSleep_1140765316(ready);
		}
	}

	private void RpcWriter___Observers_SetPlayerCode_3615296227(string code)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(code);
			SendObserversRpc(26u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___SetPlayerCode_3615296227(string code)
	{
		PlayerCode = code;
	}

	private void RpcReader___Observers_SetPlayerCode_3615296227(PooledReader PooledReader0, Channel channel)
	{
		string code = PooledReader0.ReadString();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetPlayerCode_3615296227(code);
		}
	}

	private void RpcWriter___Server_SendPunch_2166136261()
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendServerRpc(27u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendPunch_2166136261()
	{
		Punch();
	}

	private void RpcReader___Server_SendPunch_2166136261(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___SendPunch_2166136261();
		}
	}

	private void RpcWriter___Observers_Punch_2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendObserversRpc(28u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___Punch_2166136261()
	{
		Avatar.Anim.SetTrigger("Punch");
		if (!base.IsOwner)
		{
			PunchSound.Play();
		}
	}

	private void RpcReader___Observers_Punch_2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized)
		{
			RpcLogic___Punch_2166136261();
		}
	}

	private void RpcWriter___Server_MarkIntroCompleted_3281254764(BasicAvatarSettings appearance)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002EAvatarFramework_002ECustomization_002EBasicAvatarSettingsFishNet_002ESerializing_002EGenerated(writer, appearance);
			SendServerRpc(29u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	private void RpcLogic___MarkIntroCompleted_3281254764(BasicAvatarSettings appearance)
	{
		HasCompletedIntro = true;
		Console.Log(SyncAccessor__003CPlayerName_003Ek__BackingField + " has completed intro");
		SetAppearance(appearance);
	}

	private void RpcReader___Server_MarkIntroCompleted_3281254764(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		BasicAvatarSettings appearance = GeneratedReaders___Internal.Read___ScheduleOne_002EAvatarFramework_002ECustomization_002EBasicAvatarSettingsFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___MarkIntroCompleted_3281254764(appearance);
		}
	}

	private void RpcWriter___Server_SendImpact_427288424(Impact impact)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002ECombat_002EImpactFishNet_002ESerializing_002EGenerated(writer, impact);
			SendServerRpc(30u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public virtual void RpcLogic___SendImpact_427288424(Impact impact)
	{
		ReceiveImpact(impact);
	}

	private void RpcReader___Server_SendImpact_427288424(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		Impact impact = GeneratedReaders___Internal.Read___ScheduleOne_002ECombat_002EImpactFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsServerInitialized && !conn.IsLocalClient)
		{
			RpcLogic___SendImpact_427288424(impact);
		}
	}

	private void RpcWriter___Observers_ReceiveImpact_427288424(Impact impact)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002ECombat_002EImpactFishNet_002ESerializing_002EGenerated(writer, impact);
			SendObserversRpc(31u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public virtual void RpcLogic___ReceiveImpact_427288424(Impact impact)
	{
		if (!impactHistory.Contains(impact.ImpactID))
		{
			impactHistory.Add(impact.ImpactID);
			float num = 1f;
			Health.TakeDamage(impact.ImpactDamage);
			if (impact.ImpactType == EImpactType.Punch)
			{
				Singleton<SFXManager>.Instance.PlayImpactSound(ImpactSoundEntity.EMaterial.Punch, impact.HitPoint, impact.ImpactForce);
			}
			else if (impact.ImpactType == EImpactType.BluntMetal)
			{
				Singleton<SFXManager>.Instance.PlayImpactSound(ImpactSoundEntity.EMaterial.BaseballBat, impact.HitPoint, impact.ImpactForce);
			}
			ProcessImpactForce(impact.HitPoint, impact.ImpactForceDirection, impact.ImpactForce * num);
		}
	}

	private void RpcReader___Observers_ReceiveImpact_427288424(PooledReader PooledReader0, Channel channel)
	{
		Impact impact = GeneratedReaders___Internal.Read___ScheduleOne_002ECombat_002EImpactFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___ReceiveImpact_427288424(impact);
		}
	}

	private void RpcWriter___Observers_Arrest_2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendObserversRpc(32u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___Arrest_2166136261()
	{
		if (!IsArrested)
		{
			if (onArrested != null)
			{
				onArrested.Invoke();
			}
			IsArrested = true;
			Debug.Log("Player arrested");
			if (Health.IsAlive && base.IsOwner)
			{
				ExitAll();
				PlayerSingleton<PlayerCamera>.Instance.AddActiveUIElement("Arrested");
				Singleton<ArrestScreen>.Instance.Open();
			}
		}
	}

	private void RpcReader___Observers_Arrest_2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___Arrest_2166136261();
		}
	}

	private void RpcWriter___Server_SendPassOut_2166136261()
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendServerRpc(33u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendPassOut_2166136261()
	{
		PassOut();
	}

	private void RpcReader___Server_SendPassOut_2166136261(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendPassOut_2166136261();
		}
	}

	private void RpcWriter___Observers_PassOut_2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendObserversRpc(34u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: true);
			writer.Store();
		}
	}

	public void RpcLogic___PassOut_2166136261()
	{
		IsUnconscious = true;
		if (onPassedOut != null)
		{
			onPassedOut.Invoke();
		}
		CapCol.enabled = false;
		SetRagdolled(ragdolled: true);
		Avatar.MiddleSpineRB.AddForce(base.transform.forward * 30f, ForceMode.VelocityChange);
		Avatar.MiddleSpineRB.AddRelativeTorque(new Vector3(0f, UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 10f, ForceMode.VelocityChange);
		if (Health.IsAlive)
		{
			if (CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None)
			{
				IsArrested = true;
			}
			if (base.IsOwner)
			{
				ExitAll();
				PlayerSingleton<PlayerCamera>.Instance.AddActiveUIElement("Passed out");
				Singleton<PassOutScreen>.Instance.Open();
			}
		}
	}

	private void RpcReader___Observers_PassOut_2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___PassOut_2166136261();
		}
	}

	private void RpcWriter___Server_SendPassOutRecovery_2166136261()
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendServerRpc(35u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendPassOutRecovery_2166136261()
	{
		PassOutRecovery();
	}

	private void RpcReader___Server_SendPassOutRecovery_2166136261(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendPassOutRecovery_2166136261();
		}
	}

	private void RpcWriter___Observers_PassOutRecovery_2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendObserversRpc(36u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: true);
			writer.Store();
		}
	}

	public void RpcLogic___PassOutRecovery_2166136261()
	{
		Debug.Log("Player recovered from pass out");
		IsUnconscious = false;
		SetRagdolled(ragdolled: false);
		CapCol.enabled = true;
		if (base.IsOwner)
		{
			Singleton<HUD>.Instance.canvas.enabled = true;
			Energy.RestoreEnergy();
			PlayerSingleton<PlayerCamera>.Instance.RemoveActiveUIElement("Passed out");
		}
		if (onPassOutRecovery != null)
		{
			onPassOutRecovery.Invoke();
		}
	}

	private void RpcReader___Observers_PassOutRecovery_2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___PassOutRecovery_2166136261();
		}
	}

	private void RpcWriter___Server_SendEquippable_Networked_3615296227(string assetPath)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(assetPath);
			SendServerRpc(37u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendEquippable_Networked_3615296227(string assetPath)
	{
		SetEquippable_Networked(assetPath);
	}

	private void RpcReader___Server_SendEquippable_Networked_3615296227(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string assetPath = PooledReader0.ReadString();
		if (base.IsServerInitialized && !conn.IsLocalClient)
		{
			RpcLogic___SendEquippable_Networked_3615296227(assetPath);
		}
	}

	private void RpcWriter___Observers_SetEquippable_Networked_3615296227(string assetPath)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(assetPath);
			SendObserversRpc(38u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___SetEquippable_Networked_3615296227(string assetPath)
	{
		Avatar.SetEquippable(assetPath);
	}

	private void RpcReader___Observers_SetEquippable_Networked_3615296227(PooledReader PooledReader0, Channel channel)
	{
		string assetPath = PooledReader0.ReadString();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetEquippable_Networked_3615296227(assetPath);
		}
	}

	private void RpcWriter___Server_SendEquippableMessage_Networked_449655107(string message, object data)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(message);
			GeneratedWriters___Internal.Write___System_002EObjectFishNet_002ESerializing_002EGenerated(writer, data);
			SendServerRpc(39u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendEquippableMessage_Networked_449655107(string message, object data)
	{
		Avatar.ReceiveEquippableMessage(message, data);
	}

	private void RpcReader___Server_SendEquippableMessage_Networked_449655107(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string message = PooledReader0.ReadString();
		object data = GeneratedReaders___Internal.Read___System_002EObjectFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendEquippableMessage_Networked_449655107(message, data);
		}
	}

	private void RpcWriter___Observers_SendEquippableMessage_Networked_Vector_1512140110(string message, Vector3 data)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(message);
			writer.WriteVector3(data);
			SendObserversRpc(40u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___SendEquippableMessage_Networked_Vector_1512140110(string message, Vector3 data)
	{
		Avatar.ReceiveEquippableMessage(message, data);
	}

	private void RpcReader___Observers_SendEquippableMessage_Networked_Vector_1512140110(PooledReader PooledReader0, Channel channel)
	{
		string message = PooledReader0.ReadString();
		Vector3 data = PooledReader0.ReadVector3();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SendEquippableMessage_Networked_Vector_1512140110(message, data);
		}
	}

	private void RpcWriter___Server_SendAnimationTrigger_3615296227(string trigger)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(trigger);
			SendServerRpc(41u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendAnimationTrigger_3615296227(string trigger)
	{
		SetAnimationTrigger_Networked(null, trigger);
	}

	private void RpcReader___Server_SendAnimationTrigger_3615296227(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string trigger = PooledReader0.ReadString();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendAnimationTrigger_3615296227(trigger);
		}
	}

	private void RpcWriter___Observers_SetAnimationTrigger_Networked_2971853958(NetworkConnection conn, string trigger)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(trigger);
			SendObserversRpc(42u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___SetAnimationTrigger_Networked_2971853958(NetworkConnection conn, string trigger)
	{
		SetAnimationTrigger(trigger);
	}

	private void RpcReader___Observers_SetAnimationTrigger_Networked_2971853958(PooledReader PooledReader0, Channel channel)
	{
		string trigger = PooledReader0.ReadString();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetAnimationTrigger_Networked_2971853958(null, trigger);
		}
	}

	private void RpcWriter___Target_SetAnimationTrigger_Networked_2971853958(NetworkConnection conn, string trigger)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(trigger);
			SendTargetRpc(43u, writer, channel, DataOrderType.Default, conn, excludeServer: false);
			writer.Store();
		}
	}

	private void RpcReader___Target_SetAnimationTrigger_Networked_2971853958(PooledReader PooledReader0, Channel channel)
	{
		string trigger = PooledReader0.ReadString();
		if (base.IsClientInitialized)
		{
			RpcLogic___SetAnimationTrigger_Networked_2971853958(base.LocalConnection, trigger);
		}
	}

	private void RpcWriter___Observers_ResetAnimationTrigger_Networked_2971853958(NetworkConnection conn, string trigger)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(trigger);
			SendObserversRpc(44u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___ResetAnimationTrigger_Networked_2971853958(NetworkConnection conn, string trigger)
	{
		ResetAnimationTrigger(trigger);
	}

	private void RpcReader___Observers_ResetAnimationTrigger_Networked_2971853958(PooledReader PooledReader0, Channel channel)
	{
		string trigger = PooledReader0.ReadString();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___ResetAnimationTrigger_Networked_2971853958(null, trigger);
		}
	}

	private void RpcWriter___Target_ResetAnimationTrigger_Networked_2971853958(NetworkConnection conn, string trigger)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(trigger);
			SendTargetRpc(45u, writer, channel, DataOrderType.Default, conn, excludeServer: false);
			writer.Store();
		}
	}

	private void RpcReader___Target_ResetAnimationTrigger_Networked_2971853958(PooledReader PooledReader0, Channel channel)
	{
		string trigger = PooledReader0.ReadString();
		if (base.IsClientInitialized)
		{
			RpcLogic___ResetAnimationTrigger_Networked_2971853958(base.LocalConnection, trigger);
		}
	}

	private void RpcWriter___Server_SendAnimationBool_310431262(string name, bool val)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(name);
			writer.WriteBoolean(val);
			SendServerRpc(46u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendAnimationBool_310431262(string name, bool val)
	{
		SetAnimationBool(name, val);
	}

	private void RpcReader___Server_SendAnimationBool_310431262(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string text = PooledReader0.ReadString();
		bool val = PooledReader0.ReadBoolean();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendAnimationBool_310431262(text, val);
		}
	}

	private void RpcWriter___Observers_SetAnimationBool_310431262(string name, bool val)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(name);
			writer.WriteBoolean(val);
			SendObserversRpc(47u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___SetAnimationBool_310431262(string name, bool val)
	{
		Avatar.Anim.SetBool(name, val);
	}

	private void RpcReader___Observers_SetAnimationBool_310431262(PooledReader PooledReader0, Channel channel)
	{
		string text = PooledReader0.ReadString();
		bool val = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetAnimationBool_310431262(text, val);
		}
	}

	private void RpcWriter___Observers_Taze_2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			SendObserversRpc(48u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___Taze_2166136261()
	{
		IsTased = true;
		if (onTased != null)
		{
			onTased.Invoke();
		}
		if (taseCoroutine != null)
		{
			StopCoroutine(taseCoroutine);
		}
		taseCoroutine = StartCoroutine(Tase());
		IEnumerator Tase()
		{
			LayerUtility.SetLayerRecursively(ZapParticles.gameObject, LayerMask.NameToLayer("Default"));
			ZapParticles.Play();
			ZapSound.Play();
			yield return new WaitForSeconds(2f);
			ZapParticles.Stop();
			IsTased = false;
			if (onTasedEnd != null)
			{
				onTasedEnd.Invoke();
			}
		}
	}

	private void RpcReader___Observers_Taze_2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized)
		{
			RpcLogic___Taze_2166136261();
		}
	}

	private void RpcWriter___Server_SetInventoryItem_2317364410(int index, ItemInstance item)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteInt32(index);
			writer.WriteItemInstance(item);
			SendServerRpc(49u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SetInventoryItem_2317364410(int index, ItemInstance item)
	{
		Inventory[index].SetStoredItem(item);
	}

	private void RpcReader___Server_SetInventoryItem_2317364410(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		int index = PooledReader0.ReadInt32();
		ItemInstance item = PooledReader0.ReadItemInstance();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SetInventoryItem_2317364410(index, item);
		}
	}

	private void RpcWriter___Server_SendAppearance_3281254764(BasicAvatarSettings settings)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002EAvatarFramework_002ECustomization_002EBasicAvatarSettingsFishNet_002ESerializing_002EGenerated(writer, settings);
			SendServerRpc(50u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendAppearance_3281254764(BasicAvatarSettings settings)
	{
		SetAppearance(settings);
	}

	private void RpcReader___Server_SendAppearance_3281254764(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		BasicAvatarSettings settings = GeneratedReaders___Internal.Read___ScheduleOne_002EAvatarFramework_002ECustomization_002EBasicAvatarSettingsFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendAppearance_3281254764(settings);
		}
	}

	private void RpcWriter___Observers_SetAppearance_3281254764(BasicAvatarSettings settings)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			GeneratedWriters___Internal.Write___ScheduleOne_002EAvatarFramework_002ECustomization_002EBasicAvatarSettingsFishNet_002ESerializing_002EGenerated(writer, settings);
			SendObserversRpc(51u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___SetAppearance_3281254764(BasicAvatarSettings settings)
	{
		CurrentAvatarSettings = settings;
		Console.Log("Setting appearance for " + SyncAccessor__003CPlayerName_003Ek__BackingField);
		AvatarSettings avatarSettings = CurrentAvatarSettings.GetAvatarSettings();
		Avatar.LoadAvatarSettings(avatarSettings);
		SetVisibleToLocalPlayer(!base.IsOwner);
	}

	private void RpcReader___Observers_SetAppearance_3281254764(PooledReader PooledReader0, Channel channel)
	{
		BasicAvatarSettings settings = GeneratedReaders___Internal.Read___ScheduleOne_002EAvatarFramework_002ECustomization_002EBasicAvatarSettingsFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetAppearance_3281254764(settings);
		}
	}

	private void RpcWriter___Server_SendMountedSkateboard_3323014238(NetworkObject skateboardObj)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			if ((object)networkManager2 == null)
			{
				networkManager2 = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager2 != null)
			{
				networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because you are not the owner of this object. .");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteNetworkObject(skateboardObj);
			SendServerRpc(52u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	private void RpcLogic___SendMountedSkateboard_3323014238(NetworkObject skateboardObj)
	{
		SetMountedSkateboard(skateboardObj);
	}

	private void RpcReader___Server_SendMountedSkateboard_3323014238(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		NetworkObject skateboardObj = PooledReader0.ReadNetworkObject();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___SendMountedSkateboard_3323014238(skateboardObj);
		}
	}

	private void RpcWriter___Observers_SetMountedSkateboard_3323014238(NetworkObject skateboardObj)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteNetworkObject(skateboardObj);
			SendObserversRpc(53u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___SetMountedSkateboard_3323014238(NetworkObject skateboardObj)
	{
		if (skateboardObj != null)
		{
			if (!(ActiveSkateboard != null))
			{
				Skateboard component = skateboardObj.GetComponent<Skateboard>();
				RpcWriter___Server_set_IsSkating_1140765316(value: true);
				ActiveSkateboard = component;
				base.transform.SetParent(component.PlayerContainer);
				base.transform.localPosition = Vector3.zero;
				base.transform.localRotation = Quaternion.identity;
				if (onSkateboardMounted != null)
				{
					onSkateboardMounted(component);
				}
			}
		}
		else if (!(ActiveSkateboard == null))
		{
			RpcWriter___Server_set_IsSkating_1140765316(value: false);
			ActiveSkateboard = null;
			base.transform.SetParent(null);
			base.transform.rotation = Quaternion.LookRotation(base.transform.forward, Vector3.up);
			base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
			if (onSkateboardDismounted != null)
			{
				onSkateboardDismounted();
			}
		}
	}

	private void RpcReader___Observers_SetMountedSkateboard_3323014238(PooledReader PooledReader0, Channel channel)
	{
		NetworkObject skateboardObj = PooledReader0.ReadNetworkObject();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___SetMountedSkateboard_3323014238(skateboardObj);
		}
	}

	private void RpcWriter___Server_SendConsumeProduct_2622925554(ProductItemInstance product)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteProductItemInstance(product);
			SendServerRpc(54u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	private void RpcLogic___SendConsumeProduct_2622925554(ProductItemInstance product)
	{
		ReceiveConsumeProduct(product);
	}

	private void RpcReader___Server_SendConsumeProduct_2622925554(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		ProductItemInstance product = PooledReader0.ReadProductItemInstance();
		if (base.IsServerInitialized)
		{
			RpcLogic___SendConsumeProduct_2622925554(product);
		}
	}

	private void RpcWriter___Observers_ReceiveConsumeProduct_2622925554(ProductItemInstance product)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteProductItemInstance(product);
			SendObserversRpc(55u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	private void RpcLogic___ReceiveConsumeProduct_2622925554(ProductItemInstance product)
	{
		if (!base.IsOwner)
		{
			ConsumeProductInternal(product);
		}
	}

	private void RpcReader___Observers_ReceiveConsumeProduct_2622925554(PooledReader PooledReader0, Channel channel)
	{
		ProductItemInstance product = PooledReader0.ReadProductItemInstance();
		if (base.IsClientInitialized)
		{
			RpcLogic___ReceiveConsumeProduct_2622925554(product);
		}
	}

	private void RpcWriter___Server_SendValue_3589193952(string variableName, string value, bool sendToOwner)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(variableName);
			writer.WriteString(value);
			writer.WriteBoolean(sendToOwner);
			SendServerRpc(56u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___SendValue_3589193952(string variableName, string value, bool sendToOwner)
	{
		if (sendToOwner || !base.IsOwner)
		{
			ReceiveValue(variableName, value);
		}
		if (sendToOwner)
		{
			ReceiveValue(base.Owner, variableName, value);
		}
	}

	private void RpcReader___Server_SendValue_3589193952(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string variableName = PooledReader0.ReadString();
		string value = PooledReader0.ReadString();
		bool sendToOwner = PooledReader0.ReadBoolean();
		if (base.IsServerInitialized && !conn.IsLocalClient)
		{
			RpcLogic___SendValue_3589193952(variableName, value, sendToOwner);
		}
	}

	private void RpcWriter___Target_ReceiveValue_3895153758(NetworkConnection conn, string variableName, string value)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null)
			{
				networkManager = InstanceFinder.NetworkManager;
			}
			if ((object)networkManager != null)
			{
				networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
			else
			{
				Debug.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			}
		}
		else
		{
			Channel channel = Channel.Reliable;
			PooledWriter writer = WriterPool.GetWriter();
			writer.WriteString(variableName);
			writer.WriteString(value);
			SendTargetRpc(57u, writer, channel, DataOrderType.Default, conn, excludeServer: false);
			writer.Store();
		}
	}

	private void RpcLogic___ReceiveValue_3895153758(NetworkConnection conn, string variableName, string value)
	{
		ReceiveValue(variableName, value);
	}

	private void RpcReader___Target_ReceiveValue_3895153758(PooledReader PooledReader0, Channel channel)
	{
		string variableName = PooledReader0.ReadString();
		string value = PooledReader0.ReadString();
		if (base.IsClientInitialized)
		{
			RpcLogic___ReceiveValue_3895153758(base.LocalConnection, variableName, value);
		}
	}

	public virtual bool ReadSyncVar___ScheduleOne_002EPlayerScripts_002EPlayer(PooledReader PooledReader0, uint UInt321, bool Boolean2)
	{
		switch (UInt321)
		{
		case 6u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CCameraRotation_003Ek__BackingField(syncVar____003CCameraRotation_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			Quaternion value2 = PooledReader0.ReadQuaternion();
			this.sync___set_value__003CCameraRotation_003Ek__BackingField(value2, Boolean2);
			return true;
		}
		case 5u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CCameraPosition_003Ek__BackingField(syncVar____003CCameraPosition_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			Vector3 value6 = PooledReader0.ReadVector3();
			this.sync___set_value__003CCameraPosition_003Ek__BackingField(value6, Boolean2);
			return true;
		}
		case 4u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CIsReadyToSleep_003Ek__BackingField(syncVar____003CIsReadyToSleep_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			bool value3 = PooledReader0.ReadBoolean();
			this.sync___set_value__003CIsReadyToSleep_003Ek__BackingField(value3, Boolean2);
			return true;
		}
		case 3u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CCurrentBed_003Ek__BackingField(syncVar____003CCurrentBed_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			NetworkObject value5 = PooledReader0.ReadNetworkObject();
			this.sync___set_value__003CCurrentBed_003Ek__BackingField(value5, Boolean2);
			return true;
		}
		case 2u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CCurrentVehicle_003Ek__BackingField(syncVar____003CCurrentVehicle_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			NetworkObject value7 = PooledReader0.ReadNetworkObject();
			this.sync___set_value__003CCurrentVehicle_003Ek__BackingField(value7, Boolean2);
			return true;
		}
		case 1u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CPlayerCode_003Ek__BackingField(syncVar____003CPlayerCode_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			string value4 = PooledReader0.ReadString();
			this.sync___set_value__003CPlayerCode_003Ek__BackingField(value4, Boolean2);
			return true;
		}
		case 0u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CPlayerName_003Ek__BackingField(syncVar____003CPlayerName_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			string value = PooledReader0.ReadString();
			this.sync___set_value__003CPlayerName_003Ek__BackingField(value, Boolean2);
			return true;
		}
		default:
			return false;
		}
	}

	protected virtual void Awake_UserLogic_ScheduleOne_002EPlayerScripts_002EPlayer_Assembly_002DCSharp_002Edll()
	{
		if (InstanceFinder.NetworkManager == null)
		{
			Local = this;
		}
		ScheduleOne.GameTime.TimeManager.onSleepStart = (Action)Delegate.Combine(ScheduleOne.GameTime.TimeManager.onSleepStart, new Action(SleepStart));
		ScheduleOne.GameTime.TimeManager.onSleepEnd = (Action<int>)Delegate.Combine(ScheduleOne.GameTime.TimeManager.onSleepEnd, new Action<int>(SleepEnd));
		Health.onDie.AddListener(OnDied);
		Health.onRevive.AddListener(OnRevived);
		Energy.onEnergyDepleted.AddListener(SendPassOut);
		InvokeRepeating("RecalculateCurrentProperty", 0f, 0.5f);
		InitializeSaveable();
		Inventory = new ItemSlot[9];
		for (int i = 0; i < Inventory.Length; i++)
		{
			Inventory[i] = new ItemSlot();
		}
		Rigidbody[] ragdollRBs = Avatar.RagdollRBs;
		foreach (Rigidbody rigidbody in ragdollRBs)
		{
			Physics.IgnoreCollision(rigidbody.GetComponent<Collider>(), CapCol, ignore: true);
			ragdollForceComponents.Add(rigidbody.gameObject.AddComponent<ConstantForce>());
		}
		SetGravityMultiplier(1f);
	}
}
