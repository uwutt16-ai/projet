using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Generated;
using FishNet.Transporting;
using ScheduleOne.DevUtilities;
using ScheduleOne.Law;
using UnityEngine;

namespace ScheduleOne.PlayerScripts;

public class PlayerVisualState : NetworkBehaviour
{
	public enum EVisualState
	{
		Visible,
		Suspicious,
		DisobeyingCurfew,
		Vandalizing,
		PettyCrime,
		DrugDealing,
		SearchedFor,
		Wanted,
		Pickpocketing,
		DischargingWeapon,
		Brandishing
	}

	[Serializable]
	public class VisualState
	{
		public EVisualState state;

		public string label;

		public Action stateDestroyed;
	}

	public float Suspiciousness;

	public List<VisualState> visualStates = new List<VisualState>();

	private Player player;

	private Dictionary<string, Coroutine> removalRoutinesDict = new Dictionary<string, Coroutine>();

	private bool NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerVisualStateAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerVisualStateAssembly_002DCSharp_002Edll_Excuted;

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_ScheduleOne_002EPlayerScripts_002EPlayerVisualState_Assembly_002DCSharp_002Edll();
		NetworkInitialize__Late();
	}

	private void Update()
	{
		if (NetworkSingleton<CurfewManager>.InstanceExists && NetworkSingleton<CurfewManager>.Instance.IsCurrentlyActiveWithTolerance && player.CurrentProperty == null && player.CurrentBusiness == null)
		{
			if (GetState("DisobeyingCurfew") == null)
			{
				ApplyState("DisobeyingCurfew", EVisualState.DisobeyingCurfew);
			}
		}
		else if (GetState("DisobeyingCurfew") != null)
		{
			RemoveState("DisobeyingCurfew");
		}
		UpdateSuspiciousness();
	}

	[ServerRpc(RunLocally = true)]
	public void ApplyState(string label, EVisualState state, float autoRemoveAfter = 0f)
	{
		RpcWriter___Server_ApplyState_868472085(label, state, autoRemoveAfter);
		RpcLogic___ApplyState_868472085(label, state, autoRemoveAfter);
	}

	[ServerRpc(RunLocally = true)]
	public void RemoveState(string label, float delay = 0f)
	{
		RpcWriter___Server_RemoveState_606697822(label, delay);
		RpcLogic___RemoveState_606697822(label, delay);
	}

	public VisualState GetState(string label)
	{
		return visualStates.Find((VisualState x) => x.label == label);
	}

	public void ClearStates()
	{
		VisualState[] array = visualStates.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (!(array[i].label == "Visible"))
			{
				RemoveState(array[i].label);
			}
		}
	}

	private void UpdateSuspiciousness()
	{
		Suspiciousness = 0f;
		if (player.Avatar.Anim.IsCrouched)
		{
			Suspiciousness += 0.3f;
		}
		if (player.Avatar.CurrentEquippable != null)
		{
			Suspiciousness += player.Avatar.CurrentEquippable.Suspiciousness;
		}
		if (player.VelocityCalculator.Velocity.magnitude > PlayerMovement.WalkSpeed)
		{
			Suspiciousness += 0.3f * Mathf.InverseLerp(PlayerMovement.WalkSpeed, PlayerMovement.WalkSpeed * PlayerMovement.SprintMultiplier, player.VelocityCalculator.Velocity.magnitude);
		}
		Suspiciousness = Mathf.Clamp01(Suspiciousness);
	}

	public virtual void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerVisualStateAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyScheduleOne_002EPlayerScripts_002EPlayerVisualStateAssembly_002DCSharp_002Edll_Excuted = true;
			RegisterServerRpc(0u, RpcReader___Server_ApplyState_868472085);
			RegisterServerRpc(1u, RpcReader___Server_RemoveState_606697822);
		}
	}

	public virtual void NetworkInitialize__Late()
	{
		if (!NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerVisualStateAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize__LateScheduleOne_002EPlayerScripts_002EPlayerVisualStateAssembly_002DCSharp_002Edll_Excuted = true;
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize__Late();
	}

	private void RpcWriter___Server_ApplyState_868472085(string label, EVisualState state, float autoRemoveAfter = 0f)
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
			writer.WriteString(label);
			GeneratedWriters___Internal.Write___ScheduleOne_002EPlayerScripts_002EPlayerVisualState_002FEVisualStateFishNet_002ESerializing_002EGenerated(writer, state);
			writer.WriteSingle(autoRemoveAfter);
			SendServerRpc(0u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___ApplyState_868472085(string label, EVisualState state, float autoRemoveAfter = 0f)
	{
		VisualState visualState = GetState(label);
		if (visualState == null)
		{
			visualState = new VisualState();
			visualState.label = label;
			visualStates.Add(visualState);
		}
		visualState.state = state;
		if (removalRoutinesDict.ContainsKey(label))
		{
			Singleton<CoroutineService>.Instance.StopCoroutine(removalRoutinesDict[label]);
			removalRoutinesDict.Remove(label);
		}
		if (autoRemoveAfter > 0f)
		{
			RemoveState(label, autoRemoveAfter);
		}
	}

	private void RpcReader___Server_ApplyState_868472085(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string label = PooledReader0.ReadString();
		EVisualState state = GeneratedReaders___Internal.Read___ScheduleOne_002EPlayerScripts_002EPlayerVisualState_002FEVisualStateFishNet_002ESerializing_002EGenerateds(PooledReader0);
		float autoRemoveAfter = PooledReader0.ReadSingle();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___ApplyState_868472085(label, state, autoRemoveAfter);
		}
	}

	private void RpcWriter___Server_RemoveState_606697822(string label, float delay = 0f)
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
			writer.WriteString(label);
			writer.WriteSingle(delay);
			SendServerRpc(1u, writer, channel, DataOrderType.Default);
			writer.Store();
		}
	}

	public void RpcLogic___RemoveState_606697822(string label, float delay = 0f)
	{
		VisualState newState = GetState(label);
		if (newState == null)
		{
			return;
		}
		if (delay > 0f)
		{
			if (removalRoutinesDict.ContainsKey(label))
			{
				Singleton<CoroutineService>.Instance.StopCoroutine(removalRoutinesDict[label]);
				removalRoutinesDict.Remove(label);
			}
			removalRoutinesDict.Add(label, Singleton<CoroutineService>.Instance.StartCoroutine(DelayedRemove()));
		}
		else
		{
			Destroy();
		}
		IEnumerator DelayedRemove()
		{
			yield return new WaitForSeconds(delay);
			Destroy();
			removalRoutinesDict.Remove(label);
		}
		void Destroy()
		{
			if (newState.stateDestroyed != null)
			{
				newState.stateDestroyed();
			}
			visualStates.Remove(newState);
		}
	}

	private void RpcReader___Server_RemoveState_606697822(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		string label = PooledReader0.ReadString();
		float delay = PooledReader0.ReadSingle();
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___RemoveState_606697822(label, delay);
		}
	}

	private void Awake_UserLogic_ScheduleOne_002EPlayerScripts_002EPlayerVisualState_Assembly_002DCSharp_002Edll()
	{
		player = GetComponent<Player>();
		player.Health.onDie.AddListener(delegate
		{
			ClearStates();
		});
		ApplyState("Visible", EVisualState.Visible);
	}
}
