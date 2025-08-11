using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using FishNet.Serializing.Generated;
using FishNet.Transporting;
using ScheduleOne.Audio;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Law;
using ScheduleOne.Levelling;
using ScheduleOne.Map;
using ScheduleOne.NPCs;
using ScheduleOne.Police;
using ScheduleOne.UI;
using UnityEngine;

namespace ScheduleOne.PlayerScripts;

public class PlayerCrimeData : NetworkBehaviour
{
	public class VehicleCollisionInstance
	{
		public NPC Victim;

		public float TimeSince;

		public VehicleCollisionInstance(NPC victim, float timeSince)
		{
			Victim = victim;
			TimeSince = timeSince;
		}
	}

	public enum EPursuitLevel
	{
		None,
		Investigating,
		Arresting,
		NonLethal,
		Lethal
	}

	public const float SEARCH_TIME_INVESTIGATING = 60f;

	public const float SEARCH_TIME_ARRESTING = 25f;

	public const float SEARCH_TIME_NONLETHAL = 30f;

	public const float SEARCH_TIME_LETHAL = 40f;

	public const float ESCALATION_TIME_ARRESTING = 25f;

	public const float ESCALATION_TIME_NONLETHAL = 120f;

	public const float SHOT_COOLDOWN_MIN = 2f;

	public const float SHOT_COOLDOWN_MAX = 8f;

	public const float VEHICLE_COLLISION_LIFETIME = 30f;

	public const float VEHICLE_COLLISION_LIMIT = 3f;

	public PoliceOfficer NearestOfficer;

	public Player Player;

	public AudioSourceController onPursuitEscapedSound;

	[CompilerGenerated]
	[SyncVar(SendRate = 0.5f, WritePermissions = WritePermission.ClientUnsynchronized)]
	public EPursuitLevel _003CCurrentPursuitLevel_003Ek__BackingField;

	[CompilerGenerated]
	[SyncVar(SendRate = 0.5f, WritePermissions = WritePermission.ClientUnsynchronized)]
	public Vector3 _003CLastKnownPosition_003Ek__BackingField;

	public List<PoliceOfficer> Pursuers = new List<PoliceOfficer>();

	public float TimeSincePursuitStart;

	public float CurrentPursuitLevelDuration;

	public float TimeSinceSighted = 100000f;

	public Dictionary<Crime, int> Crimes = new Dictionary<Crime, int>();

	public bool BodySearchPending;

	public float timeSinceLastShot = 1000f;

	protected List<VehicleCollisionInstance> Collisions = new List<VehicleCollisionInstance>();

	private MusicTrack _lightCombatTrack;

	private MusicTrack _heavyCombatTrack;

	private float outOfSightTimeToDipMusic = 8f;

	private float minMusicVolume = 0.6f;

	private float musicChangeRate_Down = 0.04f;

	private float musicChangeRate_Up = 2f;

	public SyncVar<EPursuitLevel> syncVar____003CCurrentPursuitLevel_003Ek__BackingField;

	public SyncVar<Vector3> syncVar____003CLastKnownPosition_003Ek__BackingField;

	private bool NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerCrimeDataAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerCrimeDataAssembly_002DCSharp_002Edll_Excuted;

	public EPursuitLevel CurrentPursuitLevel
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField;
		}
		[CompilerGenerated]
		[ServerRpc(RunLocally = true)]
		protected set
		{
			RpcWriter___Server_set_CurrentPursuitLevel_2979171596(value);
			RpcLogic___set_CurrentPursuitLevel_2979171596(value);
		}
	}

	public Vector3 LastKnownPosition
	{
		[CompilerGenerated]
		get
		{
			return SyncAccessor__003CLastKnownPosition_003Ek__BackingField;
		}
		[CompilerGenerated]
		[ServerRpc(RunLocally = true)]
		protected set
		{
			RpcWriter___Server_set_LastKnownPosition_4276783012(value);
			RpcLogic___set_LastKnownPosition_4276783012(value);
		}
	} = Vector3.zero;

	public float CurrentArrestProgress { get; protected set; }

	public float CurrentBodySearchProgress { get; protected set; }

	public float TimeSinceLastBodySearch { get; set; } = 100000f;

	public bool EvadedArrest { get; protected set; }

	public EPursuitLevel SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField
	{
		get
		{
			return CurrentPursuitLevel;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				CurrentPursuitLevel = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CCurrentPursuitLevel_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public Vector3 SyncAccessor__003CLastKnownPosition_003Ek__BackingField
	{
		get
		{
			return LastKnownPosition;
		}
		set
		{
			if (value || !base.IsServerInitialized)
			{
				LastKnownPosition = value;
			}
			if (Application.isPlaying)
			{
				syncVar____003CLastKnownPosition_003Ek__BackingField.SetValue(value, value);
			}
		}
	}

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_ScheduleOne_002EPlayerScripts_002EPlayerCrimeData_Assembly_002DCSharp_002Edll();
		NetworkInitialize__Late();
	}

	private void Start()
	{
		NetworkSingleton<ScheduleOne.GameTime.TimeManager>.Instance._onSleepStart.RemoveListener(OnSleepStart);
		NetworkSingleton<ScheduleOne.GameTime.TimeManager>.Instance._onSleepStart.AddListener(OnSleepStart);
	}

	private void OnDestroy()
	{
		if (NetworkSingleton<ScheduleOne.GameTime.TimeManager>.InstanceExists)
		{
			NetworkSingleton<ScheduleOne.GameTime.TimeManager>.Instance._onSleepStart.RemoveListener(OnSleepStart);
		}
	}

	protected virtual void Update()
	{
		CurrentPursuitLevelDuration += Time.deltaTime;
		TimeSincePursuitStart += Time.deltaTime;
		TimeSinceSighted += Time.deltaTime;
		timeSinceLastShot += Time.deltaTime;
		TimeSinceLastBodySearch += Time.deltaTime;
		if (!Player.IsOwner)
		{
			return;
		}
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField != EPursuitLevel.None && SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField != EPursuitLevel.Lethal)
		{
			UpdateEscalation();
		}
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField != EPursuitLevel.None)
		{
			UpdateTimeout();
			UpdateMusic();
		}
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField != EPursuitLevel.None && TimeSinceSighted > 2f)
		{
			Player.VisualState.ApplyState("SearchedFor", PlayerVisualState.EVisualState.SearchedFor);
		}
		else
		{
			Player.VisualState.RemoveState("SearchedFor");
		}
		for (int i = 0; i < Collisions.Count; i++)
		{
			Collisions[i].TimeSince += Time.deltaTime;
			if (Collisions[i].TimeSince > 30f)
			{
				Collisions.RemoveAt(i);
				i--;
			}
		}
		Singleton<HUD>.Instance.CrimeStatusUI.UpdateStatus();
		if ((float)Collisions.Count >= 3f)
		{
			RecordLastKnownPosition(resetTimeSinceSighted: true);
			SetPursuitLevel(EPursuitLevel.Investigating);
			AddCrime(new VehicularAssault(), Collisions.Count - 1);
			Singleton<LawManager>.Instance.PoliceCalled(Player, new VehicularAssault());
			Collisions.Clear();
		}
	}

	protected virtual void LateUpdate()
	{
		if (CurrentArrestProgress > 0f)
		{
			Singleton<ProgressSlider>.Instance.Configure("Cuffing...", new Color32(75, 165, byte.MaxValue, byte.MaxValue));
			Singleton<ProgressSlider>.Instance.ShowProgress(CurrentArrestProgress);
		}
		else if (CurrentBodySearchProgress > 0f)
		{
			Singleton<ProgressSlider>.Instance.Configure("Being searched...", new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			Singleton<ProgressSlider>.Instance.ShowProgress(CurrentBodySearchProgress);
		}
		CurrentArrestProgress = 0f;
		CurrentBodySearchProgress = 0f;
	}

	public void SetPursuitLevel(EPursuitLevel level)
	{
		Debug.Log("New pursuit level: " + level);
		EPursuitLevel num = SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField;
		CurrentPursuitLevel = level;
		if (num == EPursuitLevel.None && level != EPursuitLevel.None)
		{
			TimeSincePursuitStart = 0f;
			TimeSinceSighted = 0f;
			Player.VisualState.ApplyState("Wanted", PlayerVisualState.EVisualState.Wanted);
			if (Player.Owner.IsLocalClient)
			{
				_lightCombatTrack.Enable();
			}
		}
		if (level == EPursuitLevel.Lethal && Player.Owner.IsLocalClient)
		{
			_lightCombatTrack.Stop();
			_heavyCombatTrack.Enable();
		}
		if (num != EPursuitLevel.None && level == EPursuitLevel.None)
		{
			ClearCrimes();
			Player.VisualState.RemoveState("Wanted");
			if (Player.Owner.IsLocalClient)
			{
				_lightCombatTrack.Disable();
				_lightCombatTrack.Stop();
				_heavyCombatTrack.Disable();
				_heavyCombatTrack.Stop();
			}
		}
		CurrentPursuitLevelDuration = 0f;
		if (Player.IsOwner)
		{
			Singleton<HUD>.Instance.CrimeStatusUI.UpdateStatus();
		}
	}

	public void Escalate()
	{
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.None)
		{
			SetPursuitLevel(EPursuitLevel.Investigating);
		}
		else if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.Investigating)
		{
			SetPursuitLevel(EPursuitLevel.Arresting);
		}
		else if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.Arresting)
		{
			SetEvaded();
			SetPursuitLevel(EPursuitLevel.NonLethal);
			if (PoliceStation.GetClosestPoliceStation(Player.Avatar.MiddleSpineRB.position).TimeSinceLastDispatch > 10f)
			{
				PoliceStation.GetClosestPoliceStation(Player.Avatar.MiddleSpineRB.position).Dispatch(1, Player, PoliceStation.EDispatchType.Auto, beginAsSighted: true);
			}
		}
		else if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.NonLethal)
		{
			SetPursuitLevel(EPursuitLevel.Lethal);
			PoliceStation.GetClosestPoliceStation(Player.Avatar.MiddleSpineRB.position);
			PoliceStation.GetClosestPoliceStation(Player.Avatar.MiddleSpineRB.position).Dispatch(1, Player, PoliceStation.EDispatchType.Auto, beginAsSighted: true);
		}
	}

	public void Deescalate()
	{
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.Investigating)
		{
			SetPursuitLevel(EPursuitLevel.None);
		}
		else if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.Arresting)
		{
			SetPursuitLevel(EPursuitLevel.Investigating);
		}
		else if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.NonLethal)
		{
			SetPursuitLevel(EPursuitLevel.Arresting);
		}
		else if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.Lethal)
		{
			SetPursuitLevel(EPursuitLevel.NonLethal);
		}
	}

	[ObserversRpc(RunLocally = true)]
	public void RecordLastKnownPosition(bool resetTimeSinceSighted)
	{
		RpcWriter___Observers_RecordLastKnownPosition_1140765316(resetTimeSinceSighted);
		RpcLogic___RecordLastKnownPosition_1140765316(resetTimeSinceSighted);
	}

	public void SetArrestProgress(float progress)
	{
		CurrentArrestProgress = progress;
		if (progress >= 1f)
		{
			Player.Arrest();
			SetPursuitLevel(EPursuitLevel.None);
		}
	}

	public void ResetBodysearchCooldown()
	{
		TimeSinceLastBodySearch = 0f;
	}

	public void SetBodySearchProgress(float progress)
	{
		CurrentBodySearchProgress = progress;
		if (CurrentBodySearchProgress >= 1f)
		{
			TimeSinceLastBodySearch = 0f;
			BodySearchPending = false;
		}
	}

	private void OnDie()
	{
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField != EPursuitLevel.None)
		{
			SetArrestProgress(1f);
		}
	}

	public void AddCrime(Crime crime, int quantity = 1)
	{
		if (crime == null)
		{
			return;
		}
		Debug.Log("Adding crime: " + crime);
		Crime[] array = Crimes.Keys.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].GetType() == crime.GetType())
			{
				Crimes[array[i]] += quantity;
				return;
			}
		}
		Crimes.Add(crime, quantity);
	}

	public void ClearCrimes()
	{
		Crimes.Clear();
		EvadedArrest = false;
	}

	public bool IsCrimeOnRecord(Type crime)
	{
		Crime[] array = Crimes.Keys.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].GetType() == crime)
			{
				return true;
			}
		}
		return false;
	}

	public void SetEvaded()
	{
		EvadedArrest = true;
	}

	private void OnSleepStart()
	{
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField != EPursuitLevel.None)
		{
			SetPursuitLevel(EPursuitLevel.None);
			ClearCrimes();
		}
	}

	private void UpdateEscalation()
	{
		if (TimeSinceSighted > 1f)
		{
			return;
		}
		if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.Arresting)
		{
			if (CurrentPursuitLevelDuration > 25f)
			{
				Escalate();
			}
		}
		else if (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField == EPursuitLevel.NonLethal && CurrentPursuitLevelDuration > 120f)
		{
			Escalate();
		}
	}

	private void UpdateTimeout()
	{
		if (Player.IsOwner && TimeSinceSighted > GetSearchTime() + 3f)
		{
			TimeoutPursuit();
		}
	}

	private void UpdateMusic()
	{
		if (Player.Owner.IsLocalClient)
		{
			float volumeMultiplier = _lightCombatTrack.VolumeMultiplier;
			volumeMultiplier = ((!(TimeSinceSighted > outOfSightTimeToDipMusic)) ? (volumeMultiplier + musicChangeRate_Up * Time.deltaTime) : (volumeMultiplier - musicChangeRate_Down * Time.deltaTime));
			volumeMultiplier = Mathf.Clamp(volumeMultiplier, minMusicVolume, 1f);
			_lightCombatTrack.VolumeMultiplier = volumeMultiplier;
			_heavyCombatTrack.VolumeMultiplier = volumeMultiplier;
		}
	}

	private void TimeoutPursuit()
	{
		switch (SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField)
		{
		case EPursuitLevel.Arresting:
			NetworkSingleton<LevelManager>.Instance.AddXP(20);
			break;
		case EPursuitLevel.NonLethal:
			NetworkSingleton<LevelManager>.Instance.AddXP(40);
			break;
		case EPursuitLevel.Lethal:
			NetworkSingleton<LevelManager>.Instance.AddXP(60);
			break;
		}
		onPursuitEscapedSound.Play();
		SetPursuitLevel(EPursuitLevel.None);
		ClearCrimes();
	}

	public float GetSearchTime()
	{
		return SyncAccessor__003CCurrentPursuitLevel_003Ek__BackingField switch
		{
			EPursuitLevel.Investigating => 60f, 
			EPursuitLevel.Arresting => 25f, 
			EPursuitLevel.NonLethal => 30f, 
			EPursuitLevel.Lethal => 40f, 
			_ => 0f, 
		};
	}

	public void ResetShotAccuracy()
	{
		timeSinceLastShot = 0f;
	}

	public float GetShotAccuracyMultiplier()
	{
		float num = 1f;
		if (timeSinceLastShot < 2f)
		{
			num = 0f;
		}
		if (timeSinceLastShot < 8f)
		{
			num = 1f - (timeSinceLastShot - 2f) / 6f;
		}
		float t = Mathf.Clamp01(Mathf.InverseLerp(0f, PlayerMovement.WalkSpeed * PlayerMovement.SprintMultiplier, Player.VelocityCalculator.Velocity.magnitude));
		float num2 = Mathf.Lerp(2f, 0.5f, t);
		int num3 = 0;
		for (int i = 0; i < PoliceOfficer.Officers.Count; i++)
		{
			if (PoliceOfficer.Officers[i].PursuitBehaviour.Active && PoliceOfficer.Officers[i].TargetPlayerNOB == Player.NetworkObject && Vector3.Distance(PoliceOfficer.Officers[i].transform.position, Player.Avatar.CenterPoint) < 20f)
			{
				num3++;
			}
		}
		float num4 = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((float)num3 / 3f));
		return num * num2 * num4;
	}

	public void RecordVehicleCollision(NPC victim)
	{
		VehicleCollisionInstance item = new VehicleCollisionInstance(victim, 0f);
		Collisions.Add(item);
	}

	private void CheckNearestOfficer()
	{
		if (!(Player == null))
		{
			NearestOfficer = PoliceOfficer.Officers.OrderBy((PoliceOfficer x) => Vector3.Distance(x.Avatar.CenterPoint, Player.Avatar.CenterPoint)).FirstOrDefault();
		}
	}

	public virtual void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerCrimeDataAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerCrimeDataAssembly_002DCSharp_002Edll_Excuted = true;
			syncVar____003CLastKnownPosition_003Ek__BackingField = new SyncVar<Vector3>(this, 1u, WritePermission.ClientUnsynchronized, ReadPermission.Observers, 0.5f, Channel.Reliable, LastKnownPosition);
			syncVar____003CCurrentPursuitLevel_003Ek__BackingField = new SyncVar<EPursuitLevel>(this, 0u, WritePermission.ClientUnsynchronized, ReadPermission.Observers, 0.5f, Channel.Reliable, CurrentPursuitLevel);
			RegisterServerRpc(0u, RpcReader___Server_set_CurrentPursuitLevel_2979171596);
			RegisterServerRpc(1u, RpcReader___Server_set_LastKnownPosition_4276783012);
			RegisterObserversRpc(2u, RpcReader___Observers_RecordLastKnownPosition_1140765316);
			RegisterSyncVarRead(ReadSyncVar___ScheduleOne_002EPlayerScripts_002EPlayerCrimeData);
		}
	}

	public virtual void NetworkInitialize__Late()
	{
		if (!NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerCrimeDataAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerCrimeDataAssembly_002DCSharp_002Edll_Excuted = true;
			syncVar____003CLastKnownPosition_003Ek__BackingField.SetRegistered();
			syncVar____003CCurrentPursuitLevel_003Ek__BackingField.SetRegistered();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize__Late();
	}

	private void RpcWriter___Server_set_CurrentPursuitLevel_2979171596(EPursuitLevel value)
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
			GeneratedWriters___Internal.Write___ScheduleOne_002EPlayerScripts_002EPlayerCrimeData_002FEPursuitLevelFishNet_002ESerializing_002EGenerated(writer, value);
			SendServerRpc(0u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	[SpecialName]
	protected void RpcLogic___set_CurrentPursuitLevel_2979171596(EPursuitLevel value)
	{
		this.sync___set_value__003CCurrentPursuitLevel_003Ek__BackingField(value, asServer: true);
	}

	private void RpcReader___Server_set_CurrentPursuitLevel_2979171596(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		EPursuitLevel value = GeneratedReaders___Internal.Read___ScheduleOne_002EPlayerScripts_002EPlayerCrimeData_002FEPursuitLevelFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___set_CurrentPursuitLevel_2979171596(value);
		}
	}

	private void RpcWriter___Server_set_LastKnownPosition_4276783012(Vector3 value)
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
			SendServerRpc(1u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	[SpecialName]
	protected void RpcLogic___set_LastKnownPosition_4276783012(Vector3 value)
	{
		this.sync___set_value__003CLastKnownPosition_003Ek__BackingField(value, asServer: true);
	}

	private void RpcReader___Server_set_LastKnownPosition_4276783012(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		Vector3 value = PooledReader0.ReadVector3();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___set_LastKnownPosition_4276783012(value);
		}
	}

	private void RpcWriter___Observers_RecordLastKnownPosition_1140765316(bool resetTimeSinceSighted)
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
			writer.WriteBoolean(resetTimeSinceSighted);
			SendObserversRpc(2u, writer, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
			writer.Store();
		}
	}

	public void RpcLogic___RecordLastKnownPosition_1140765316(bool resetTimeSinceSighted)
	{
		LastKnownPosition = Player.Avatar.CenterPoint;
		if (resetTimeSinceSighted)
		{
			TimeSinceSighted = 0f;
		}
	}

	private void RpcReader___Observers_RecordLastKnownPosition_1140765316(PooledReader PooledReader0, Channel channel)
	{
		bool resetTimeSinceSighted = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized && !base.IsHost)
		{
			RpcLogic___RecordLastKnownPosition_1140765316(resetTimeSinceSighted);
		}
	}

	public virtual bool ReadSyncVar___ScheduleOne_002EPlayerScripts_002EPlayerCrimeData(PooledReader PooledReader0, uint UInt321, bool Boolean2)
	{
		switch (UInt321)
		{
		case 1u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CLastKnownPosition_003Ek__BackingField(syncVar____003CLastKnownPosition_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			Vector3 value2 = PooledReader0.ReadVector3();
			this.sync___set_value__003CLastKnownPosition_003Ek__BackingField(value2, Boolean2);
			return true;
		}
		case 0u:
		{
			if (PooledReader0 == null)
			{
				this.sync___set_value__003CCurrentPursuitLevel_003Ek__BackingField(syncVar____003CCurrentPursuitLevel_003Ek__BackingField.GetValue(calledByUser: true), asServer: true);
				return true;
			}
			EPursuitLevel value = GeneratedReaders___Internal.Read___ScheduleOne_002EPlayerScripts_002EPlayerCrimeData_002FEPursuitLevelFishNet_002ESerializing_002EGenerateds(PooledReader0);
			this.sync___set_value__003CCurrentPursuitLevel_003Ek__BackingField(value, Boolean2);
			return true;
		}
		default:
			return false;
		}
	}

	private void Awake_UserLogic_ScheduleOne_002EPlayerScripts_002EPlayerCrimeData_Assembly_002DCSharp_002Edll()
	{
		Player.Health.onDie.AddListener(OnDie);
		Player.onFreed.AddListener(ClearCrimes);
		Player.onFreed.AddListener(delegate
		{
			SetPursuitLevel(EPursuitLevel.None);
		});
		InvokeRepeating("CheckNearestOfficer", 0f, 0.2f);
		_lightCombatTrack = Singleton<MusicPlayer>.Instance.Tracks.Find((MusicTrack t) => t.TrackName == "Light Combat");
		_heavyCombatTrack = Singleton<MusicPlayer>.Instance.Tracks.Find((MusicTrack t) => t.TrackName == "Heavy Combat");
	}
}
