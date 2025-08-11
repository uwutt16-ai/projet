using System;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.PlayerScripts.Health;
using ScheduleOne.UI;
using UnityEngine;
using UnityEngine.Events;

namespace ScheduleOne.PlayerScripts;

public class PlayerEnergy : MonoBehaviour
{
	public const float CRITICAL_THRESHOLD = 20f;

	public const float MAX_ENERGY = 100f;

	public const float SPRINT_DRAIN_MULTIPLIER = 1.3f;

	public bool DEBUG_DISABLE_ENERGY;

	[Header("Settings")]
	public float EnergyDuration_Hours = 22f;

	public float EnergyRechargeTime_Hours = 6f;

	public UnityEvent onEnergyChanged;

	public UnityEvent onEnergyDepleted;

	public float CurrentEnergy { get; protected set; } = 100f;

	public int EnergyDrinksConsumed { get; protected set; }

	protected virtual void Start()
	{
		TimeManager instance = NetworkSingleton<TimeManager>.Instance;
		instance.onMinutePass = (Action)Delegate.Combine(instance.onMinutePass, new Action(MinPass));
		Singleton<SleepCanvas>.Instance.onSleepFullyFaded.AddListener(SleepEnd);
		GetComponent<PlayerHealth>().onRevive.AddListener(ResetEnergyDrinks);
	}

	private void MinPass()
	{
		if ((!DEBUG_DISABLE_ENERGY || (!Debug.isDebugBuild && !Application.isEditor)) && !NetworkSingleton<TimeManager>.Instance.SleepInProgress)
		{
			float num = (0f - 1f / (EnergyDuration_Hours * 60f)) * 100f;
			if (PlayerSingleton<PlayerMovement>.Instance.isSprinting)
			{
				num *= 1.3f;
			}
			ChangeEnergy(num);
		}
	}

	private void ChangeEnergy(float change)
	{
		SetEnergy(CurrentEnergy + change);
	}

	public void SetEnergy(float newEnergy)
	{
	}

	public void RestoreEnergy()
	{
		SetEnergy(100f);
	}

	private void SleepEnd()
	{
		ResetEnergyDrinks();
		RestoreEnergy();
	}

	public void IncrementEnergyDrinks()
	{
		EnergyDrinksConsumed++;
	}

	private void ResetEnergyDrinks()
	{
		EnergyDrinksConsumed = 0;
	}
}
