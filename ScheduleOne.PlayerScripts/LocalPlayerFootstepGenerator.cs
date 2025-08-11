using ScheduleOne.DevUtilities;
using ScheduleOne.Materials;
using UnityEngine;
using UnityEngine.Events;

namespace ScheduleOne.PlayerScripts;

public class LocalPlayerFootstepGenerator : MonoBehaviour
{
	public float DistancePerStep = 0.75f;

	public Transform ReferencePoint;

	public LayerMask GroundDetectionMask;

	public UnityEvent<EMaterialType, float> onStep = new UnityEvent<EMaterialType, float>();

	private float currentDistance;

	private Vector3 lastFramePosition = Vector3.zero;

	private void LateUpdate()
	{
		if (!PlayerSingleton<PlayerMovement>.InstanceExists)
		{
			return;
		}
		if (!PlayerSingleton<PlayerMovement>.Instance.canMove)
		{
			currentDistance = 0f;
			lastFramePosition = base.transform.position;
			return;
		}
		Vector3 position = base.transform.position;
		currentDistance += Vector3.Distance(position, lastFramePosition) * (PlayerSingleton<PlayerMovement>.Instance.isSprinting ? 0.75f : 1f);
		if (currentDistance >= DistancePerStep)
		{
			currentDistance = 0f;
			lastFramePosition = position;
			TriggerStep();
		}
		lastFramePosition = position;
	}

	public void TriggerStep()
	{
		if (IsGrounded(out var surfaceType))
		{
			onStep.Invoke(surfaceType, PlayerSingleton<PlayerMovement>.Instance.isSprinting ? 1f : 0.5f);
		}
	}

	public bool IsGrounded(out EMaterialType surfaceType)
	{
		surfaceType = EMaterialType.Generic;
		if (Physics.Raycast(ReferencePoint.position + Vector3.up * 0.1f, Vector3.down, out var hitInfo, 0.25f, GroundDetectionMask, QueryTriggerInteraction.Ignore))
		{
			MaterialTag componentInParent = hitInfo.collider.GetComponentInParent<MaterialTag>();
			if (componentInParent != null)
			{
				surfaceType = componentInParent.MaterialType;
			}
			return true;
		}
		return false;
	}
}
