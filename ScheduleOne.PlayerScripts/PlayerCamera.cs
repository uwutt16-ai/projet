using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScheduleOne.Audio;
using ScheduleOne.DevUtilities;
using ScheduleOne.EntityFramework;
using ScheduleOne.FX;
using ScheduleOne.Tools;
using ScheduleOne.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ScheduleOne.PlayerScripts;

public class PlayerCamera : PlayerSingleton<PlayerCamera>
{
	public enum ECameraMode
	{
		Default,
		Vehicle,
		Skateboard
	}

	public const float CAMERA_SHAKE_MULTIPLIER = 0.1f;

	[Header("Settings")]
	public float cameraOffsetFromTop = -0.15f;

	public float SprintFoVBoost = 1.15f;

	public float FoVChangeRate = 4f;

	public float HorizontalCameraBob = 1f;

	public float VerticalCameraBob = 1f;

	public float BobRate = 10f;

	public AnimationCurve HorizontalBobCurve;

	public AnimationCurve VerticalBobCurve;

	public float FreeCamSpeed = 1f;

	public float FreeCamAcceleration = 2f;

	public bool SmoothLook;

	public float SmoothLookSpeed = 1f;

	public FloatSmoother FoVChangeSmoother;

	public FloatSmoother SmoothLookSmoother;

	[Header("References")]
	public Transform CameraContainer;

	public Camera Camera;

	public Animator Animator;

	public AnimationClip[] JoltClips;

	public UniversalRenderPipelineAsset[] URPAssets;

	public Transform ViewAvatarCameraPosition;

	public HeartbeatSoundController HeartbeatSoundController;

	public ParticleSystem Flies;

	[HideInInspector]
	public bool blockNextStopTransformOverride;

	private Volume globalVolume;

	private DepthOfField DoF;

	private Coroutine cameraShakeCoroutine;

	private Vector3 cameraLocalPos = Vector3.zero;

	private Vector3 freeCamMovement = Vector3.zero;

	private Coroutine focusRoutine;

	private float focusMouseX;

	private float focusMouseY;

	private Dictionary<int, PlayerMovement.MovementEvent> movementEvents = new Dictionary<int, PlayerMovement.MovementEvent>();

	private float freeCamSpeed = 1f;

	private float mouseX;

	private float mouseY;

	private List<Vector3> gizmos = new List<Vector3>();

	private Vector3 cameralocalPos_PriorOverride = Vector3.zero;

	private Quaternion cameraLocalRot_PriorOverride = Quaternion.identity;

	public Coroutine ILerpCamera_Coroutine;

	private Coroutine lookRoutine;

	private Coroutine DoFCoroutine;

	private Coroutine ILerpCameraFOV_Coroutine;

	public static ScheduleOne.DevUtilities.GraphicsSettings.EAntiAliasingMode AntiAliasingMode { get; private set; }

	public bool canLook { get; protected set; } = true;

	public int activeUIElementCount => activeUIElements.Count;

	public bool transformOverriden { get; protected set; }

	public bool fovOverriden { get; protected set; }

	public bool FreeCamEnabled { get; private set; }

	public bool ViewingAvatar { get; private set; }

	public ECameraMode CameraMode { get; protected set; }

	public List<string> activeUIElements { get; protected set; } = new List<string>();

	protected override void Awake()
	{
		base.Awake();
		Player.onLocalPlayerSpawned = (Action)Delegate.Remove(Player.onLocalPlayerSpawned, new Action(PlayerSpawned));
		Player.onLocalPlayerSpawned = (Action)Delegate.Combine(Player.onLocalPlayerSpawned, new Action(PlayerSpawned));
		GameInput.RegisterExitListener(Exit, 100);
		ApplyAASettings();
	}

	public override void OnStartClient(bool IsOwner)
	{
		base.OnStartClient(IsOwner);
		if (!IsOwner)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
		else
		{
			Camera.enabled = true;
		}
	}

	protected override void Start()
	{
		base.Start();
		if (Singleton<Settings>.InstanceExists)
		{
			Camera.fieldOfView = Singleton<Settings>.Instance.CameraFOV;
		}
		if (GameObject.Find("GlobalVolume") != null)
		{
			globalVolume = GameObject.Find("GlobalVolume").GetComponent<Volume>();
			globalVolume.sharedProfile.TryGet<DepthOfField>(out DoF);
			DoF.active = false;
		}
		cameralocalPos_PriorOverride = base.transform.localPosition;
		Singleton<EnvironmentFX>.Instance.HeightFog.mainCamera = Camera;
		FoVChangeSmoother.Initialize();
		FoVChangeSmoother.SetDefault(0f);
		SmoothLookSmoother.Initialize();
		SmoothLookSmoother.SetDefault(0f);
		SmoothLookSmoother.SetSmoothingSpeed(0.5f);
		LockMouse();
	}

	private void PlayerSpawned()
	{
		Player.Local.onTased.AddListener(delegate
		{
			StartCameraShake(1f, 2f);
		});
		Player.Local.onTasedEnd.AddListener(StopCameraShake);
	}

	public static void SetAntialiasingMode(ScheduleOne.DevUtilities.GraphicsSettings.EAntiAliasingMode mode)
	{
		AntiAliasingMode = mode;
		if (PlayerSingleton<PlayerCamera>.Instance != null)
		{
			PlayerSingleton<PlayerCamera>.Instance.ApplyAASettings();
		}
	}

	public void ApplyAASettings()
	{
		AntialiasingMode antialiasingMode = AntialiasingMode.None;
		antialiasingMode = AntiAliasingMode switch
		{
			ScheduleOne.DevUtilities.GraphicsSettings.EAntiAliasingMode.Off => AntialiasingMode.None, 
			ScheduleOne.DevUtilities.GraphicsSettings.EAntiAliasingMode.FXAA => AntialiasingMode.FastApproximateAntialiasing, 
			ScheduleOne.DevUtilities.GraphicsSettings.EAntiAliasingMode.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing, 
			_ => AntialiasingMode.None, 
		};
		Camera.GetComponent<UniversalAdditionalCameraData>().antialiasing = antialiasingMode;
	}

	protected virtual void Update()
	{
		UpdateCameraBob();
		if (canLook)
		{
			RotateCamera();
		}
		if (FreeCamEnabled)
		{
			RotateFreeCam();
			UpdateFreeCamInput();
			MoveFreeCam();
		}
		if (GameInput.GetButton(GameInput.ButtonCode.ViewAvatar))
		{
			if (!ViewingAvatar && activeUIElementCount == 0 && canLook && !GameInput.IsTyping)
			{
				ViewAvatar();
			}
			if (ViewingAvatar)
			{
				Vector3 worldPos = ViewAvatarCameraPosition.position;
				Vector3 vector = PlayerSingleton<PlayerMovement>.Instance.transform.TransformPoint(new Vector3(0f, GetTargetLocalY(), 0f));
				if (Physics.Raycast(vector, (ViewAvatarCameraPosition.position - vector).normalized, out var hitInfo, Vector3.Distance(vector, ViewAvatarCameraPosition.position), 1 << LayerMask.NameToLayer("Default"), QueryTriggerInteraction.Ignore))
				{
					worldPos = hitInfo.point;
				}
				OverrideTransform(worldPos, ViewAvatarCameraPosition.rotation, 0f, keepParented: true);
				base.transform.LookAt(Player.Local.Avatar.LowestSpine.transform);
			}
		}
		else if (ViewingAvatar)
		{
			StopViewingAvatar();
		}
		if ((FreeCamEnabled || Application.isEditor) && Input.GetKeyDown(KeyCode.F12))
		{
			Screenshot();
		}
		UpdateMovementEvents();
	}

	private void Screenshot()
	{
		StartCoroutine(Routine());
		static IEnumerator Routine()
		{
			yield return new WaitForEndOfFrame();
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			folderPath = Path.Combine(folderPath, "Screenshot_" + DateTime.Now.ToString("HH-mm-ss") + ".png");
			Console.Log("Screenshot saved to: " + folderPath);
			ScreenCapture.CaptureScreenshot(folderPath, 2);
			yield return new WaitForEndOfFrame();
		}
	}

	protected virtual void LateUpdate()
	{
		if (!(Camera == null) && !(base.transform == null) && PlayerSingleton<PlayerMovement>.InstanceExists)
		{
			if (!transformOverriden && ILerpCamera_Coroutine == null)
			{
				base.transform.localPosition = new Vector3(0f, GetTargetLocalY(), 0f);
			}
			if (!fovOverriden && ILerpCameraFOV_Coroutine == null)
			{
				float num = Singleton<Settings>.Instance.CameraFOV * (PlayerSingleton<PlayerMovement>.Instance.isSprinting ? SprintFoVBoost : 1f);
				num += FoVChangeSmoother.CurrentValue;
				Camera.fieldOfView = Mathf.MoveTowards(Camera.fieldOfView, num, Time.deltaTime * FoVChangeRate);
			}
			Camera.transform.localPosition = cameraLocalPos;
			cameraLocalPos = Vector3.zero;
		}
	}

	private void Exit(ExitAction action)
	{
		if (!action.used)
		{
			if (FreeCamEnabled && action.exitType == ExitType.Escape)
			{
				action.used = true;
				SetFreeCam(enable: false);
			}
			if (ViewingAvatar && action.exitType == ExitType.Escape)
			{
				action.used = true;
				StopViewingAvatar();
			}
		}
	}

	public float GetTargetLocalY()
	{
		if (!PlayerSingleton<PlayerMovement>.InstanceExists)
		{
			return 0f;
		}
		return PlayerSingleton<PlayerMovement>.Instance.Controller.height / 2f + cameraOffsetFromTop;
	}

	public void SetCameraMode(ECameraMode mode)
	{
		CameraMode = mode;
	}

	private void RotateCamera()
	{
		float b = GameInput.MouseDelta.x * (Singleton<Settings>.InstanceExists ? Singleton<Settings>.Instance.LookSensitivity : 1f);
		float num = GameInput.MouseDelta.y * (Singleton<Settings>.InstanceExists ? Singleton<Settings>.Instance.LookSensitivity : 1f);
		if (Player.Local.Disoriented)
		{
			num = 0f - num;
		}
		if (SmoothLook)
		{
			mouseX = Mathf.Lerp(mouseX, b, SmoothLookSpeed * Time.deltaTime);
			mouseY = Mathf.Lerp(mouseY, num, SmoothLookSpeed * Time.deltaTime);
		}
		else if (SmoothLookSmoother.CurrentValue <= 0.01f)
		{
			mouseX = b;
			mouseY = num;
		}
		else
		{
			float num2 = Mathf.Lerp(50f, 1f, SmoothLookSmoother.CurrentValue);
			mouseX = Mathf.Lerp(mouseX, b, num2 * Time.deltaTime);
			mouseY = Mathf.Lerp(mouseY, num, num2 * Time.deltaTime);
		}
		Vector3 eulerAngles = base.transform.localRotation.eulerAngles;
		Vector3 eulerAngles2 = Player.Local.transform.rotation.eulerAngles;
		if (Singleton<Settings>.InstanceExists && Singleton<Settings>.Instance.InvertMouse)
		{
			mouseY = 0f - mouseY;
		}
		mouseX += focusMouseX;
		mouseY += focusMouseY;
		eulerAngles.x -= Mathf.Clamp(mouseY, -89f, 89f);
		eulerAngles2.y += mouseX;
		eulerAngles.z = 0f;
		if (eulerAngles.x >= 180f)
		{
			if (eulerAngles.x < 271f)
			{
				eulerAngles.x = 271f;
			}
		}
		else if (eulerAngles.x > 89f)
		{
			eulerAngles.x = 89f;
		}
		base.transform.localRotation = Quaternion.Euler(eulerAngles);
		base.transform.localEulerAngles = new Vector3(base.transform.localEulerAngles.x, 0f, 0f);
		Player.Local.transform.rotation = Quaternion.Euler(eulerAngles2);
	}

	public void LockMouse()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
		if (Singleton<HUD>.InstanceExists)
		{
			Singleton<HUD>.Instance.SetCrosshairVisible(vis: true);
		}
	}

	public void FreeMouse()
	{
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		if (Singleton<HUD>.InstanceExists)
		{
			Singleton<HUD>.Instance.SetCrosshairVisible(vis: false);
		}
	}

	public bool LookRaycast(float range, out RaycastHit hit, LayerMask layerMask, bool includeTriggers = true, float radius = 0f)
	{
		if (radius == 0f)
		{
			return Physics.Raycast(base.transform.position, base.transform.forward, out hit, range, layerMask, (!includeTriggers) ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
		}
		return Physics.SphereCast(base.transform.position, radius, base.transform.forward, out hit, range, layerMask, (!includeTriggers) ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
	}

	public bool LookRaycast_ExcludeBuildables(float range, out RaycastHit hit, LayerMask layerMask, bool includeTriggers = true)
	{
		RaycastHit[] array = Physics.RaycastAll(base.transform.position, base.transform.forward, range, layerMask, (!includeTriggers) ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
		RaycastHit raycastHit = default(RaycastHit);
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].collider.GetComponentInParent<BuildableItem>() && (raycastHit.collider == null || Vector3.Distance(base.transform.position, array[i].point) < Vector3.Distance(base.transform.position, raycastHit.point)))
			{
				raycastHit = array[i];
			}
		}
		if (raycastHit.collider != null)
		{
			hit = raycastHit;
			return true;
		}
		hit = default(RaycastHit);
		return false;
	}

	private void OnDrawGizmosSelected()
	{
		for (int i = 0; i < gizmos.Count; i++)
		{
			Gizmos.DrawSphere(gizmos[i], 0.05f);
		}
		gizmos.Clear();
	}

	public bool Raycast_ExcludeBuildables(Vector3 origin, Vector3 direction, float range, out RaycastHit hit, LayerMask layerMask, bool includeTriggers = false, float radius = 0f, float maxAngleDifference = 0f)
	{
		RaycastHit[] array = ((radius != 0f) ? Physics.SphereCastAll(origin, radius, direction, range, layerMask, (!includeTriggers) ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide) : Physics.RaycastAll(origin, direction, range, layerMask, (!includeTriggers) ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide));
		_ = 0f;
		RaycastHit raycastHit = default(RaycastHit);
		for (int i = 0; i < array.Length; i++)
		{
			if (!(array[i].point == Vector3.zero) && !array[i].collider.GetComponentInParent<BuildableItem>() && (maxAngleDifference == 0f || Vector3.Angle(direction, -array[i].normal) < maxAngleDifference) && (raycastHit.collider == null || Vector3.Distance(base.transform.position, array[i].point) < Vector3.Distance(base.transform.position, raycastHit.point)))
			{
				raycastHit = array[i];
			}
		}
		if (raycastHit.collider != null)
		{
			hit = raycastHit;
			return true;
		}
		hit = default(RaycastHit);
		return false;
	}

	public bool MouseRaycast(float range, out RaycastHit hit, LayerMask layerMask, bool includeTriggers = true, float radius = 0f)
	{
		Ray ray = PlayerSingleton<PlayerCamera>.Instance.Camera.ScreenPointToRay(Input.mousePosition);
		if (radius == 0f)
		{
			return Physics.Raycast(ray, out hit, range, layerMask, (!includeTriggers) ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
		}
		return Physics.SphereCast(ray, radius, out hit, range, layerMask, (!includeTriggers) ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
	}

	public bool LookSpherecast(float range, float radius, out RaycastHit hit, LayerMask layerMask)
	{
		return Physics.SphereCast(base.transform.position, radius, base.transform.forward, out hit, range, layerMask);
	}

	public void OverrideTransform(Vector3 worldPos, Quaternion rot, float lerpTime, bool keepParented = false)
	{
		canLook = false;
		if (ILerpCamera_Coroutine != null)
		{
			StopCoroutine(ILerpCamera_Coroutine);
			ILerpCamera_Coroutine = null;
		}
		else if (!transformOverriden)
		{
			cameralocalPos_PriorOverride = base.transform.localPosition;
			cameraLocalRot_PriorOverride = base.transform.localRotation;
		}
		transformOverriden = true;
		if (!keepParented)
		{
			base.transform.SetParent(null);
		}
		ILerpCamera_Coroutine = Singleton<CoroutineService>.Instance.StartCoroutine(ILerpCamera(worldPos, rot, lerpTime, worldSpace: true));
	}

	protected IEnumerator ILerpCamera(Vector3 endPos, Quaternion endRot, float lerpTime, bool worldSpace, bool returnToRestingPosition = false)
	{
		Vector3 startPos = base.transform.localPosition;
		Quaternion startRot = base.transform.rotation;
		if (worldSpace)
		{
			startPos = base.transform.position;
		}
		float elapsed = 0f;
		while (elapsed < lerpTime)
		{
			if (returnToRestingPosition)
			{
				base.transform.localPosition = Vector3.Lerp(startPos, new Vector3(0f, GetTargetLocalY(), 0f), elapsed / lerpTime);
			}
			else if (worldSpace)
			{
				base.transform.position = Vector3.Lerp(startPos, endPos, elapsed / lerpTime);
			}
			else
			{
				base.transform.localPosition = Vector3.Lerp(startPos, endPos, elapsed / lerpTime);
			}
			base.transform.rotation = Quaternion.Lerp(startRot, endRot, elapsed / lerpTime);
			elapsed += Time.deltaTime;
			yield return new WaitForEndOfFrame();
		}
		if (returnToRestingPosition)
		{
			base.transform.localPosition = new Vector3(0f, GetTargetLocalY(), 0f);
		}
		else if (worldSpace)
		{
			base.transform.position = endPos;
		}
		else
		{
			base.transform.localPosition = endPos;
		}
		base.transform.rotation = endRot;
		ILerpCamera_Coroutine = null;
	}

	public void StopTransformOverride(float lerpTime, bool reenableCameraLook = true, bool returnToOriginalRotation = true)
	{
		if (blockNextStopTransformOverride)
		{
			blockNextStopTransformOverride = false;
			return;
		}
		if (ILerpCamera_Coroutine != null)
		{
			StopCoroutine(ILerpCamera_Coroutine);
			ILerpCamera_Coroutine = null;
		}
		if (reenableCameraLook)
		{
			if (lerpTime == 0f)
			{
				SetCanLook_True();
			}
			else
			{
				Invoke("SetCanLook_True", lerpTime);
			}
		}
		transformOverriden = false;
		base.transform.SetParent(PlayerSingleton<PlayerMovement>.Instance.transform);
		if (ILerpCamera_Coroutine != null)
		{
			StopCoroutine(ILerpCamera_Coroutine);
		}
		Quaternion quaternion = PlayerSingleton<PlayerMovement>.Instance.transform.rotation * cameraLocalRot_PriorOverride;
		if (!returnToOriginalRotation)
		{
			quaternion = base.transform.rotation;
		}
		if (lerpTime == 0f)
		{
			base.transform.rotation = quaternion;
			base.transform.localPosition = new Vector3(0f, GetTargetLocalY(), 0f);
		}
		else
		{
			ILerpCamera_Coroutine = StartCoroutine(ILerpCamera(cameralocalPos_PriorOverride, quaternion, lerpTime, worldSpace: false, returnToRestingPosition: true));
		}
	}

	public void LookAt(Vector3 point, float duration = 0.25f)
	{
		if (lookRoutine != null)
		{
			StopCoroutine(lookRoutine);
		}
		StartCoroutine(Look());
		IEnumerator Look()
		{
			Vector3 vector = Player.Local.transform.InverseTransformDirection((point - base.transform.position).normalized);
			float y = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
			Quaternion playerEndRot = Player.Local.transform.rotation * Quaternion.Euler(0f, y, 0f);
			float value = (0f - Mathf.Atan2(vector.y, vector.z)) * 57.29578f;
			value = Mathf.Clamp(value, -89f, 89f);
			Quaternion cameraRotation = Quaternion.Euler(value, 0f, 0f);
			Quaternion playerStartRot = Player.Local.transform.rotation;
			Quaternion cameraStartRot = base.transform.localRotation;
			for (float i = 0f; i < duration; i += Time.deltaTime)
			{
				Player.Local.transform.rotation = Quaternion.Lerp(playerStartRot, playerEndRot, i / duration);
				base.transform.localRotation = Quaternion.Lerp(cameraStartRot, cameraRotation, i / duration);
				yield return new WaitForEndOfFrame();
			}
			Player.Local.transform.rotation = playerEndRot;
			base.transform.localRotation = cameraRotation;
			lookRoutine = null;
		}
	}

	private void SetCanLook_True()
	{
		SetCanLook(c: true);
	}

	public void SetCanLook(bool c)
	{
		canLook = c;
	}

	public void SetDoFActive(bool active, float lerpTime)
	{
		if (DoFCoroutine != null)
		{
			StopCoroutine(DoFCoroutine);
		}
		DoFCoroutine = StartCoroutine(LerpDoF(active, lerpTime));
	}

	private IEnumerator LerpDoF(bool active, float lerpTime)
	{
		if (active)
		{
			DoF.active = true;
		}
		float startFocusDist = DoF.focusDistance.value;
		float endFocusDist = ((!active) ? 5f : 0.1f);
		for (float i = 0f; i < lerpTime; i += Time.unscaledDeltaTime)
		{
			DoF.focusDistance.value = Mathf.Lerp(startFocusDist, endFocusDist, i / lerpTime);
			yield return new WaitForEndOfFrame();
		}
		DoF.focusDistance.value = endFocusDist;
		if (!active)
		{
			DoF.active = false;
		}
		DoFCoroutine = null;
	}

	public void OverrideFOV(float fov, float lerpTime)
	{
		if (ILerpCameraFOV_Coroutine != null)
		{
			StopCoroutine(ILerpCameraFOV_Coroutine);
		}
		fovOverriden = true;
		if (fov == -1f)
		{
			fov = Singleton<Settings>.Instance.CameraFOV;
		}
		ILerpCameraFOV_Coroutine = StartCoroutine(ILerpFOV(fov, lerpTime));
	}

	protected IEnumerator ILerpFOV(float endFov, float lerpTime)
	{
		float startFov = Camera.fieldOfView;
		for (float i = 0f; i < lerpTime; i += Time.deltaTime)
		{
			Camera.fieldOfView = Mathf.Lerp(startFov, endFov, i / lerpTime);
			yield return new WaitForEndOfFrame();
		}
		Camera.fieldOfView = endFov;
		ILerpCameraFOV_Coroutine = null;
	}

	public void StopFOVOverride(float lerpTime)
	{
		OverrideFOV(-1f, lerpTime);
		fovOverriden = false;
	}

	public void AddActiveUIElement(string name)
	{
		if (!activeUIElements.Contains(name))
		{
			activeUIElements.Add(name);
		}
	}

	public void RemoveActiveUIElement(string name)
	{
		if (activeUIElements.Contains(name))
		{
			activeUIElements.Remove(name);
		}
	}

	public void RegisterMovementEvent(int threshold, Action action)
	{
		if (threshold < 1)
		{
			Console.LogWarning("Movement events min. threshold is 1m!");
			return;
		}
		if (!movementEvents.ContainsKey(threshold))
		{
			movementEvents.Add(threshold, new PlayerMovement.MovementEvent());
		}
		movementEvents[threshold].actions.Add(action);
	}

	public void DeregisterMovementEvent(Action action)
	{
		foreach (int key in movementEvents.Keys)
		{
			PlayerMovement.MovementEvent movementEvent = movementEvents[key];
			if (movementEvent.actions.Contains(action))
			{
				movementEvent.actions.Remove(action);
				break;
			}
		}
	}

	private void UpdateMovementEvents()
	{
		foreach (int item in movementEvents.Keys.ToList())
		{
			PlayerMovement.MovementEvent movementEvent = movementEvents[item];
			if (Vector3.Distance(base.transform.position, movementEvent.LastUpdatedDistance) > (float)item)
			{
				movementEvent.Update(base.transform.position);
			}
		}
	}

	private void ViewAvatar()
	{
		ViewingAvatar = true;
		AddActiveUIElement("View avatar");
		Vector3 worldPos = ViewAvatarCameraPosition.position;
		Vector3 vector = PlayerSingleton<PlayerMovement>.Instance.transform.TransformPoint(new Vector3(0f, GetTargetLocalY(), 0f));
		if (Physics.Raycast(vector, (ViewAvatarCameraPosition.position - vector).normalized, out var hitInfo, Vector3.Distance(vector, ViewAvatarCameraPosition.position), 1 << LayerMask.NameToLayer("Default"), QueryTriggerInteraction.Ignore))
		{
			worldPos = hitInfo.point;
		}
		OverrideTransform(worldPos, ViewAvatarCameraPosition.rotation, 0f, keepParented: true);
		base.transform.LookAt(Player.Local.Avatar.LowestSpine.transform);
		Singleton<HUD>.Instance.canvas.enabled = false;
		PlayerSingleton<PlayerInventory>.Instance.SetViewmodelVisible(visible: false);
		Player.Local.SetVisibleToLocalPlayer(vis: true);
	}

	private void StopViewingAvatar()
	{
		ViewingAvatar = false;
		RemoveActiveUIElement("View avatar");
		StopTransformOverride(0f);
		Singleton<HUD>.Instance.canvas.enabled = true;
		PlayerSingleton<PlayerInventory>.Instance.SetViewmodelVisible(visible: true);
		Player.Local.SetVisibleToLocalPlayer(vis: false);
	}

	public void JoltCamera()
	{
		AnimationClip animationClip = JoltClips[UnityEngine.Random.Range(0, JoltClips.Length)];
		Animator.Play(animationClip.name, 0, 0f);
	}

	public bool PointInCameraView(Vector3 point)
	{
		Vector3 vector = Camera.WorldToViewportPoint(point);
		bool num = Is01(vector.x) && Is01(vector.y);
		bool flag = vector.z > 0f;
		bool flag2 = false;
		if (Physics.Raycast(direction: (point - Camera.transform.position).normalized, maxDistance: Vector3.Distance(Camera.transform.position, point) + 0.05f, origin: Camera.transform.position, hitInfo: out var hitInfo, layerMask: 1 << LayerMask.NameToLayer("Default")) && hitInfo.point != point)
		{
			flag2 = true;
		}
		if (num && flag)
		{
			return !flag2;
		}
		return false;
	}

	public bool Is01(float a)
	{
		if (a > 0f)
		{
			return a < 1f;
		}
		return false;
	}

	public void ResetRotation()
	{
		base.transform.localRotation = Quaternion.identity;
	}

	public void FocusCameraOnTarget(Transform target)
	{
		if (focusRoutine != null)
		{
			StopCoroutine(focusRoutine);
		}
		focusRoutine = StartCoroutine(FocusRoutine());
		IEnumerator FocusRoutine()
		{
			for (float duration = 0f; duration < 0.75f; duration += Time.deltaTime)
			{
				if (!canLook)
				{
					break;
				}
				if (CameraMode != ECameraMode.Default)
				{
					break;
				}
				Vector3 vector = target.position - base.transform.position;
				Vector3 vector2 = new Vector3(vector.x, 0f, vector.z);
				Vector3 vector3 = new Vector3(0f, vector.y, 0f);
				Vector3 normalized = (target.position - base.transform.position).normalized;
				if (Vector3.Angle(base.transform.forward, normalized) < 5f || duration > 0.5f)
				{
					focusMouseX = 0f;
					focusMouseY = 0f;
					break;
				}
				float num = Vector3.SignedAngle(Vector3.ProjectOnPlane(base.transform.forward, Vector3.up), Vector3.ProjectOnPlane(normalized, Vector3.up), Vector3.up);
				Vector3 normalized2 = (PlayerSingleton<PlayerMovement>.Instance.transform.TransformPoint(new Vector3(0f, vector3.magnitude, vector2.magnitude)) - base.transform.position).normalized;
				float num2 = Vector3.SignedAngle(base.transform.forward, normalized2, base.transform.right);
				if (Mathf.Abs(num) > 5f)
				{
					focusMouseX = num * 0.1f;
				}
				else
				{
					focusMouseX = 0f;
				}
				if (Mathf.Abs(num2) > 5f)
				{
					focusMouseY = (0f - num2) * 0.1f;
				}
				else
				{
					focusMouseY = 0f;
				}
				yield return new WaitForEndOfFrame();
			}
			focusMouseX = 0f;
			focusMouseY = 0f;
		}
	}

	public void StopFocus()
	{
		if (focusRoutine != null)
		{
			StopCoroutine(focusRoutine);
		}
		focusMouseX = 0f;
		focusMouseY = 0f;
	}

	public void StartCameraShake(float intensity, float duration = -1f, bool decreaseOverTime = true)
	{
		StopCameraShake();
		cameraShakeCoroutine = StartCoroutine(Shake());
		IEnumerator Shake()
		{
			float timeRemaining = duration;
			while (true)
			{
				float num = intensity;
				if (duration != -1f && decreaseOverTime)
				{
					num *= timeRemaining / duration;
				}
				cameraLocalPos += UnityEngine.Random.insideUnitSphere * num * 0.1f;
				timeRemaining -= Time.deltaTime;
				if (timeRemaining <= 0f && duration != -1f)
				{
					break;
				}
				yield return new WaitForEndOfFrame();
			}
			Camera.transform.localPosition = Vector3.zero;
			cameraShakeCoroutine = null;
		}
	}

	public void StopCameraShake()
	{
		if (cameraShakeCoroutine != null)
		{
			StopCoroutine(cameraShakeCoroutine);
			Camera.transform.localPosition = Vector3.zero;
		}
	}

	public void UpdateCameraBob()
	{
		float num = 1f;
		if (PlayerSingleton<PlayerMovement>.InstanceExists)
		{
			num = PlayerSingleton<PlayerMovement>.Instance.CurrentSprintMultiplier - 1f;
		}
		num *= Singleton<Settings>.Instance.CameraBobIntensity;
		cameraLocalPos.x += HorizontalBobCurve.Evaluate(Time.time * BobRate % 1f) * num * HorizontalCameraBob;
		cameraLocalPos.y += VerticalBobCurve.Evaluate(Time.time * BobRate % 1f) * num * VerticalCameraBob;
	}

	public void SetFreeCam(bool enable, bool reenableLook = true)
	{
		FreeCamEnabled = enable;
		Singleton<HUD>.Instance.canvas.enabled = !enable;
		PlayerSingleton<PlayerMovement>.Instance.canMove = !enable;
		Player.Local.SetVisibleToLocalPlayer(enable);
		if (enable)
		{
			OverrideTransform(base.transform.position, base.transform.rotation, 0f);
			PlayerSingleton<PlayerCamera>.Instance.AddActiveUIElement(base.name);
		}
		else
		{
			PlayerSingleton<PlayerCamera>.Instance.RemoveActiveUIElement(base.name);
			StopTransformOverride(0f, reenableLook);
			freeCamMovement = Vector3.zero;
		}
	}

	private void RotateFreeCam()
	{
		mouseX = Mathf.Lerp(mouseX, GameInput.MouseDelta.x * (Singleton<Settings>.InstanceExists ? Singleton<Settings>.Instance.LookSensitivity : 1f), SmoothLookSpeed * Time.deltaTime);
		mouseY = Mathf.Lerp(mouseY, GameInput.MouseDelta.y * (Singleton<Settings>.InstanceExists ? Singleton<Settings>.Instance.LookSensitivity : 1f), SmoothLookSpeed * Time.deltaTime);
		Vector3 eulerAngles = base.transform.localRotation.eulerAngles;
		_ = base.transform.localRotation.eulerAngles;
		if (Singleton<Settings>.InstanceExists && Singleton<Settings>.Instance.InvertMouse)
		{
			mouseY = 0f - mouseY;
		}
		eulerAngles.x -= Mathf.Clamp(mouseY, -89f, 89f);
		eulerAngles.y += mouseX;
		eulerAngles.z = 0f;
		if (eulerAngles.x >= 180f)
		{
			if (eulerAngles.x < 271f)
			{
				eulerAngles.x = 271f;
			}
		}
		else if (eulerAngles.x > 89f)
		{
			eulerAngles.x = 89f;
		}
		base.transform.localRotation = Quaternion.Euler(eulerAngles);
		base.transform.localEulerAngles = new Vector3(base.transform.localEulerAngles.x, base.transform.localEulerAngles.y, 0f);
	}

	private void UpdateFreeCamInput()
	{
		int num = Mathf.RoundToInt(GameInput.MotionAxis.x);
		int num2 = Mathf.RoundToInt(GameInput.MotionAxis.y);
		int num3 = 0;
		if (GameInput.GetButton(GameInput.ButtonCode.Jump))
		{
			num3 = 1;
		}
		else if (GameInput.GetButton(GameInput.ButtonCode.Crouch))
		{
			num3 = -1;
		}
		if (GameInput.IsTyping)
		{
			num = 0;
			num2 = 0;
			num3 = 0;
		}
		freeCamSpeed += Input.mouseScrollDelta.y * Time.deltaTime;
		freeCamSpeed = Mathf.Clamp(freeCamSpeed, 0f, 10f);
		freeCamMovement = new Vector3(Mathf.MoveTowards(freeCamMovement.x, num, Time.unscaledDeltaTime * FreeCamAcceleration), Mathf.MoveTowards(freeCamMovement.y, num3, Time.unscaledDeltaTime * FreeCamAcceleration), Mathf.MoveTowards(freeCamMovement.z, num2, Time.unscaledDeltaTime * FreeCamAcceleration));
	}

	private void MoveFreeCam()
	{
		base.transform.position += base.transform.TransformVector(freeCamMovement) * FreeCamSpeed * freeCamSpeed * Time.unscaledDeltaTime * (GameInput.GetButton(GameInput.ButtonCode.Sprint) ? 3f : 1f);
	}
}
