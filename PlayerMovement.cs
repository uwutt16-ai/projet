using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ScheduleOne.DevUtilities;
using ScheduleOne.FX;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using ScheduleOne.Vision;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace ScheduleOne.PlayerScripts;

public class PlayerMovement : PlayerSingleton<PlayerMovement>
{
	public class MovementEvent
	{
		public List<Action> actions = new List<Action>();

		public Vector3 LastUpdatedDistance = Vector3.zero;

		public void Update(Vector3 newPosition)
		{
			LastUpdatedDistance = newPosition;
			foreach (Action action in actions)
			{
				action();
			}
		}
	}

	public const float DEV_SPRINT_MULTIPLIER = 1f;

	public const float GROUNDED_THRESHOLD = 0.05f;

	public const float SLOPE_THRESHOLD = 5f;

	public static float WalkSpeed = 3.25f;

	public static float SprintMultiplier = 1.9f;

	public static float StaticMoveSpeedMultiplier = 1f;

	public const float StaminaRestoreDelay = 1f;

	public static float JumpMultiplier = 1f;

	public static float ControllerRadius = 0.35f;

	public static float StandingControllerHeight = 1.85f;

	public static float CrouchHeightMultiplier = 0.65f;

	public static float CrouchTime = 0.2f;

	public const float StaminaDrainRate = 12.5f;

	public const float StaminaRestoreRate = 25f;

	public static float StaminaReserveMax = 100f;

	public const float SprintChangeRate = 4f;

	[Header("References")]
	public Player Player;

	public CharacterController Controller;

	[Header("Move settings")]
	[SerializeField]
	protected float sensitivity = 6f;

	[SerializeField]
	protected float dead = 0.001f;

	public bool canMove = true;

	public bool canJump = true;

	public bool SprintingRequiresStamina = true;

	public float MoveSpeedMultiplier = 1f;

	[Header("Jump/fall settings")]
	[SerializeField]
	protected float jumpForce = 4.5f;

	[SerializeField]
	protected float gravityMultiplier = 1f;

	[SerializeField]
	protected LayerMask groundDetectionMask;

	[Header("Slope Settings")]
	[SerializeField]
	protected float slopeForce;

	[SerializeField]
	protected float slopeForceRayLength;

	[Header("Crouch Settings")]
	[SerializeField]
	protected float crouchSpeedMultipler = 0.5f;

	[SerializeField]
	protected float Crouched_VigIntensity = 0.8f;

	[SerializeField]
	protected float Crouched_VigSmoothness = 1f;

	[Header("Visibility Points")]
	[SerializeField]
	protected List<Transform> visibilityPointsToScale = new List<Transform>();

	private Dictionary<Transform, float> originalVisibilityPointOffsets = new Dictionary<Transform, float>();

	protected Vector3 movement = Vector3.zero;

	protected float movementY;

	public List<LandVehicle> recentlyDrivenVehicles = new List<LandVehicle>();

	private bool isJumping;

	public float CurrentStaminaReserve = StaminaReserveMax;

	public Action<float> onStaminaReserveChanged;

	public Action onJump;

	public Action onLand;

	public UnityEvent onCrouch;

	public UnityEvent onUncrouch;

	protected float horizontalAxis;

	protected float verticalAxis;

	protected float timeGrounded;

	private Dictionary<int, MovementEvent> movementEvents = new Dictionary<int, MovementEvent>();

	private float timeSinceStaminaDrain = 10000f;

	private bool sprintActive;

	private bool sprintReleased;

	private Vector3 residualVelocityDirection = Vector3.zero;

	private float residualVelocityForce;

	private float residualVelocityDuration;

	private float residualVelocityTimeRemaining;

	private bool teleport;

	private Vector3 teleportPosition = Vector3.zero;

	private List<string> sprintBlockers = new List<string>();

	private Coroutine playerRotCoroutine;

	public static float GravityMultiplier { get; set; } = 1f;

	public float playerHeight { get; protected set; }

	public Vector3 Movement => movement;

	public LandVehicle currentVehicle { get; protected set; }

	public float airTime { get; protected set; }

	public bool isCrouched { get; protected set; }

	public float standingScale { get; protected set; } = 1f;

	public bool isRagdolled { get; protected set; }

	public bool isSprinting { get; protected set; }

	public float CurrentSprintMultiplier { get; protected set; } = 1f;

	protected override void Awake()
	{
		base.Awake();
		playerHeight = Controller.height;
		Controller.detectCollisions = false;
		for (int i = 0; i < visibilityPointsToScale.Count; i++)
		{
			originalVisibilityPointOffsets.Add(visibilityPointsToScale[i], visibilityPointsToScale[i].localPosition.y);
		}
	}

	protected override void Start()
	{
		base.Start();
		Player local = Player.Local;
		local.onEnterVehicle = (Player.VehicleEvent)Delegate.Combine(local.onEnterVehicle, new Player.VehicleEvent(EnterVehicle));
		Player local2 = Player.Local;
		local2.onExitVehicle = (Player.VehicleTransformEvent)Delegate.Combine(local2.onExitVehicle, new Player.VehicleTransformEvent(ExitVehicle));
		Player.Local.Health.onRevive.AddListener(delegate
		{
			SetStamina(StaminaReserveMax, notify: false);
		});
	}

	protected virtual void Update()
	{
		UpdateHorizontalAxis();
		UpdateVerticalAxis();
		if (isCrouched)
		{
			standingScale = Mathf.MoveTowards(standingScale, 0f, Time.deltaTime / CrouchTime);
		}
		else
		{
			standingScale = Mathf.MoveTowards(standingScale, 1f, Time.deltaTime / CrouchTime);
		}
		UpdatePlayerHeight();
		if (residualVelocityTimeRemaining > 0f)
		{
			residualVelocityTimeRemaining -= Time.deltaTime;
		}
		timeSinceStaminaDrain += Time.deltaTime;
		if (timeSinceStaminaDrain > 1f && CurrentStaminaReserve < StaminaReserveMax)
		{
			ChangeStamina(25f * Time.deltaTime);
		}
		Move();
		UpdateCrouchVignetteEffect();
		UpdateMovementEvents();
	}

	private void LateUpdate()
	{
		if (teleport)
		{
			Controller.enabled = false;
			Controller.transform.position = teleportPosition;
			Controller.enabled = true;
			teleport = false;
		}
	}

	protected virtual void Move()
	{
		isSprinting = false;
		if (!Controller.enabled)  //Si le CharacterController est désactivé (par exemple dans un véhicule), ramène progressivement le multiplicateur de sprint à 1 (vitesse normale).
								  // ??? Controller.enabled    jsp il dit si dans véhicle mais juste après y'a un test  currentVehicle != null
		{
			CurrentSprintMultiplier = Mathf.MoveTowards(CurrentSprintMultiplier, 1f, Time.deltaTime * 4f); //Mathf.MoveTowards : Change la valeur progressivement au lieu d'un changement brutal
																										   //Time.deltaTime * 4f : Vitesse de transition (4 unités par seconde)
		}
		else
		{
			if (currentVehicle != null) //si vehicle arrete 
			{
				return;
			}
			bool flag = isGrounded(); // si le joueur touche le sol       --- Script manquant : isGrounded() qui appelle Player.Local.GetIsGrounded()
			if (flag)
			{
				timeGrounded += Time.deltaTime; //timeGrounded : Accumule le temps passé au sol (utile pour éviter les faux positifs
			}
			else
			{
				timeGrounded = 0f;
			}

			//gestion du saut:     
			if (canMove && canJump && flag && !isJumping && !GameInput.IsTyping && !Singleton<PauseMenu>.Instance.IsPaused && GameInput.GetButtonDown(GameInput.ButtonCode.Jump))
			//je ne comprends pas pk on teste canMove , rregarder quand la variabke est fausse pour comprebndre
			{
				if (!isCrouched)
				{
					isJumping = true;
					if (onJump != null) //regarde si abonnée a l'event 
					{
						onJump(); /// Event sans paramètre qui retourne void       jsps si gère la logique du saut donc gravité ou autre 
					}
					Player.Local.PlayJumpAnimation(); //animation 
					StartCoroutine(Jump());
				}
				else
				{
					TryToggleCrouch(); //Si accroupi → essaie de se relever au lieu de sauter
				}
			}

			//Gestion de l'accroupissement
			if (canMove && !GameInput.IsTyping && !Singleton<PauseMenu>.Instance.IsPaused && GameInput.GetButtonDown(GameInput.ButtonCode.Crouch))
			{
				TryToggleCrouch(); //active/désactive l'accroupissement.
			}
			
			if (!flag) //si en l'air
			{
				airTime += Time.deltaTime; //accumule le temps en l'air
			}
			else   //si pas en l'air
			{
				isJumping = false;
				if (airTime > 0.1f && onLand != null)
				//Le 0.1f évite les faux positifs. Sans ça, le joueur pourrait "atterrir" en marchant sur une petite bosse, ce qui déclencherait inutilement l'event d'atterrissage. 
				// Seuls les vrais sauts/chutes comptent.
				{
					onLand();
				}
				airTime = 0f;
			}

			//sprint
			if (GameInput.GetButtonDown(GameInput.ButtonCode.Sprint) && !sprintActive)
			{
				sprintActive = true;
				sprintReleased = false;
			}

			//Si mode Hold ET touche maintenue → sprint actif
			else if (GameInput.GetButton(GameInput.ButtonCode.Sprint) && Singleton<Settings>.Instance.SprintMode == InputSettings.EActionMode.Hold)
			{
				sprintActive = true;
			}
			//Si mode Hold ET touche pas maintenue → sprint inactif
			else if (Singleton<Settings>.Instance.SprintMode == InputSettings.EActionMode.Hold)
			{
				sprintActive = false;
			}

			if (!GameInput.GetButton(GameInput.ButtonCode.Sprint))
			{
				sprintReleased = true;
			}

			// Mode TOGGLE : Une pression = active/désactive
			if (GameInput.GetButtonDown(GameInput.ButtonCode.Sprint) && sprintReleased)
			{
				sprintActive = !sprintActive;
			}

			//Application du sprint et consommation de stamina
			isSprinting = false;
			if (sprintActive && canMove && !isCrouched && !Player.Local.IsTased && (horizontalAxis != 0f || verticalAxis != 0f) && sprintBlockers.Count == 0) //Aucun bloqueur de sprint
																																							  //horizontalAxis != 0f || verticalAxis != 0f)     en mouvement 
			{
				if (CurrentStaminaReserve > 0f || !SprintingRequiresStamina)
				{
					CurrentSprintMultiplier = Mathf.MoveTowards(CurrentSprintMultiplier, SprintMultiplier, Time.deltaTime * 4f); //Augmente progressivement le multiplicateur de vitess
					if (SprintingRequiresStamina)
					{
						ChangeStamina(-12.5f * Time.deltaTime);
					}
					isSprinting = true;
				}
				else
				{
					sprintActive = false;
					CurrentSprintMultiplier = Mathf.MoveTowards(CurrentSprintMultiplier, 1f, Time.deltaTime * 4f);
				}
			}
			else
			{
				sprintActive = false;
				CurrentSprintMultiplier = Mathf.MoveTowards(CurrentSprintMultiplier, 1f, Time.deltaTime * 4f);  //Ramène le multiplicateur à 1
			}


			if (!isSprinting && timeSinceStaminaDrain > 1f)
			//Ramène le multiplicateur à 1 si pas en sprint et 1s après arrêt de consommation de stamina           pas capté 'ou 
			{
				CurrentSprintMultiplier = Mathf.MoveTowards(CurrentSprintMultiplier, 1f, Time.deltaTime * 4f);
			}

			//Calcule le multiplicateur d'accroupissement
			float num = 1f;
			if (isCrouched)
			{
				//crouchSpeedMultipler 0.5f
				num = 1f - (1f - crouchSpeedMultipler) * (1f - standingScale); //standingScale varie de 0 (accroupi) à 1 (debout)
			}


			float num2 = WalkSpeed * CurrentSprintMultiplier * num * StaticMoveSpeedMultiplier * MoveSpeedMultiplier;


			if (Player.Local.IsTased)
			{
				num2 *= 0.5f;
			}

			if ((Application.isEditor || Debug.isDebugBuild) && isSprinting)
			{
				num2 *= 1f;
			}

			//Application du mouvement selon l'état
			if (Controller.isGrounded)
			{
				if (canMove)
				{
					movement = new Vector3(horizontalAxis, 0f - Controller.stepOffset, verticalAxis); //0f - Controller.stepOffset : Permet de "coller" au sol et monter les petits obstacles
					//Controller.stepOffset est la hauteur maximale que le joueur peut monter automatiquement (escaliers, petits obstacles)
					//Cela "pousse" légèrement le joueur vers le sol
					//Permet de monter les escaliers sans sautiller

					movement = base.transform.TransformDirection(movement); //TransformDirection : Convertit du local vers le monde (rotation du joueur)
// 					Pourquoi ?

// horizontalAxis/verticalAxis sont en coordonnées locales du joueur
// Si le joueur regarde vers la droite et appuie sur "Avancer", il doit avancer vers la droite dans le monde
// TransformDirection() applique la rotation du joueur au mouvement

// Exemple :

// Joueur regarde vers l'est, appuie sur "Avancer"
// Local : (0, 0, 1) (avancer en Z local)
// Monde : (1, 0, 0) (avancer en X monde = vers l'est)

// J'espère que ces explications clarifient tout ! N'hésite pas si tu as d'autres questions.


					ClampMovement(); //ClampMovement() : Limite la magnitude à 1
					movement.x *= num2;
					movement.z *= num2;
				}
				else
				{
					movement = new Vector3(0f, 0f - Controller.stepOffset, 0f);
				}
			}
			else if (canMove)
			{
				movement = new Vector3(horizontalAxis, movement.y, verticalAxis);
				movement = base.transform.TransformDirection(movement);
				ClampMovement();
				movement.x *= num2;
				movement.z *= num2;
			}
			else
			{
				movement = new Vector3(0f, movement.y, 0f);
			}

			//Si mouvement interdit, arrête progressivement au lieu d'un arrêt brutal
			if (!canMove)
			{
				movement.x = Mathf.MoveTowards(movement.x, 0f, sensitivity * Time.deltaTime);
				movement.z = Mathf.MoveTowards(movement.z, 0f, sensitivity * Time.deltaTime);
			}

			//Application de la gravité
			movement.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime * GravityMultiplier;
			movement.y += movementY; //Ajoute movementY (utilisé pour les ajustements de hauteur, saut, etc.)
			movementY = 0f; //Reset movementY pour la frame suivante


			//Applique une poussée temporaire (explosions, propulsion, etc.) qui diminue avec le temps
			if (residualVelocityTimeRemaining > 0f)
			{
				movement += residualVelocityDirection * residualVelocityForce * Mathf.Clamp01(residualVelocityTimeRemaining / residualVelocityDuration) * Time.deltaTime;
			}

			//Gestion des pentes
			float surfaceAngle = GetSurfaceAngle();
			if ((horizontalAxis != 0f || verticalAxis != 0f) && surfaceAngle > 5f) //Si en mouvement ET pente > 5° : applique une force vers le bas
			{
				float num3 = Mathf.Clamp01(surfaceAngle / Controller.slopeLimit);
				Vector3 vector = Vector3.down * Time.deltaTime * slopeForce * num3;
				Controller.Move(movement * Time.deltaTime + vector);
			}
			else
			{
				Controller.Move(movement * Time.deltaTime);
			}
// 			Calcule l'angle de la surface sous le joueur
// Si en mouvement ET pente > 5° : applique une force vers le bas
// Plus la pente est raide, plus la force est importante
// Empêche de "flotter" sur les pentes raides
		}
	}




	private void ClampMovement()
	{
		float y = movement.y;
		movement = Vector3.ClampMagnitude(new Vector3(movement.x, 0f, movement.z), 1f);
		movement.y = y;
	}

	protected float GetSurfaceAngle()
	{
		if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, slopeForceRayLength, groundDetectionMask))
		{
			return Vector3.Angle(hitInfo.normal, Vector3.up);
		}
		return 0f;
	}

	public bool isGrounded()
	{
		return Player.Local.GetIsGrounded();
	}

	protected void UpdateHorizontalAxis()
	{
		if (Singleton<PauseMenu>.Instance.IsPaused)
		{
			horizontalAxis = 0f;
			return;
		}
		int num = ((!GameInput.IsTyping) ? Mathf.RoundToInt(GameInput.MotionAxis.x) : 0);
		if (Player.Disoriented)
		{
			num = -num;
		}
		float num2 = Mathf.MoveTowards(horizontalAxis, num, sensitivity * Time.deltaTime);
		// // Applique une zone morte pour éviter les micro-mouvements
		horizontalAxis = ((Mathf.Abs(num2) < dead) ? 0f : num2);
	}

	protected void UpdateVerticalAxis()
	{
		if (Singleton<PauseMenu>.Instance.IsPaused)
		{
			verticalAxis = 0f;
			return;
		}
		int num = ((!GameInput.IsTyping) ? Mathf.RoundToInt(GameInput.MotionAxis.y) : 0);
		float num2 = Mathf.MoveTowards(verticalAxis, num, sensitivity * Time.deltaTime);
		verticalAxis = ((Mathf.Abs(num2) < dead) ? 0f : num2);
	}

	private IEnumerator Jump()
	{
		float savedSlopeLimit = Controller.slopeLimit; //// Sauvegarde la limite de pente actuelle
		//Pourquoi le sauvegarder pendant le saut ?
// Pendant le saut, Unity peut modifier cette valeur pour éviter que le joueur "s'accroche" aux murs. On la restaure après pour retrouver le comportement normal.

		Controller.velocity.Set(Controller.velocity.x, 0f, Controller.velocity.y); //  // Remet la vélocité Y à 0 (arrête la chute en cours)
		movementY += jumpForce * JumpMultiplier; //   // Ajoute la force de saut à movementY
		timeGrounded = 0f;
		// // Attend que le joueur retouche le sol OU tape le plafond OU entre dans un véhicule
		do
		{
			yield return new WaitForEndOfFrame();
		}
		while (timeGrounded < 0.05f && Controller.collisionFlags != CollisionFlags.Above && currentVehicle == null); 
		//Controller.collisionFlags indique où le CharacterController a eu des collisions cette frame :  none, below, above , sides 

		// // Restaure la limite de pente
		Controller.slopeLimit = savedSlopeLimit;

		//Pourquoi sauvegarder slopeLimit ?
		//Pendant le saut, Unity modifie parfois cette valeur. On la sauvegarde pour la restaurer après.
	}

	private void TryToggleCrouch()
	{
		if (isCrouched) //accroupi
		{
			if (CanStand()) //peut se lever
			{
				SetCrouched(c: false); 
			}
		}
		else //pas accroupi
		{
			SetCrouched(c: true);
		}
	}

	public bool CanStand()
	{
		float num = Controller.radius * 0.75f;
		float num2 = 0.1f;
		if (Physics.SphereCast(base.transform.position - Vector3.up * Controller.height * 0.5f + Vector3.up * num + Vector3.up * num2, maxDistance: playerHeight - num * 2f - num2, radius: num, direction: Vector3.up, hitInfo: out var _, layerMask: groundDetectionMask))
		{
			return false;
		}
		return true;
		//Cette fonction lance une "sphère invisible" du bas vers le haut du joueur pour vérifier s'il y a un obstacle (plafond, objet) qui empêcherait de se relever.
	}

	public void SetCrouched(bool c)
	{
		isCrouched = c;
		Player.SendCrouched(isCrouched); //   // Synchronise avec le réseau
		Player.SetCrouchedLocal(isCrouched); //// Met à jour l'avatar local

		//// Gestion de la visibilité (système de furtivité)
		VisibilityAttribute attribute = Player.Local.Visibility.GetAttribute("Crouched");
		if (isCrouched)
		{
			//   // Crée un attribut de visibilité "accroupi" (plus difficile à voir)
			if (attribute == null)
			{
				attribute = new VisibilityAttribute("Crouched", 0f, 0.8f, 1);
			}
		}
		else
		{
			// Supprime l'attribut de furtivité
			attribute?.Delete();
		}
	}

	private void UpdateCrouchVignetteEffect()
	{
		float intensity = Mathf.Lerp(Crouched_VigIntensity, Singleton<PostProcessingManager>.Instance.Vig_DefaultIntensity, standingScale);
		float smoothness = Mathf.Lerp(Crouched_VigSmoothness, Singleton<PostProcessingManager>.Instance.Vig_DefaultSmoothness, standingScale);
		Singleton<PostProcessingManager>.Instance.OverrideVignette(intensity, smoothness);
	}

	private void UpdatePlayerHeight()
	{
		float height = Controller.height;
		Controller.height = playerHeight - playerHeight * (1f - CrouchHeightMultiplier) * (1f - standingScale);
		float num = Controller.height - height;
		if (isGrounded() && Mathf.Abs(num) > 1E-05f)
		{
			movementY += num * 0.5f;
		}
		if (Mathf.Abs(num) > 0.0001f)
		{
			for (int i = 0; i < visibilityPointsToScale.Count; i++)
			{
				visibilityPointsToScale[i].localPosition = new Vector3(visibilityPointsToScale[i].localPosition.x, originalVisibilityPointOffsets[visibilityPointsToScale[i]] * (Controller.height / playerHeight), visibilityPointsToScale[i].localPosition.z);
			}
		}
	}

	public void LerpPlayerRotation(Quaternion rotation, float lerpTime)
	{
		if (playerRotCoroutine != null)
		{
			StopCoroutine(playerRotCoroutine);
		}
		playerRotCoroutine = StartCoroutine(LerpPlayerRotation_Process(rotation, lerpTime));
	}

	private IEnumerator LerpPlayerRotation_Process(Quaternion endRotation, float lerpTime)
	{
		Quaternion startRot = Player.transform.rotation;
		Controller.enabled = false;
		for (float i = 0f; i < lerpTime; i += Time.deltaTime)
		{
			Player.transform.rotation = Quaternion.Lerp(startRot, endRotation, i / lerpTime);
			yield return new WaitForEndOfFrame();
		}
		Player.transform.rotation = endRotation;
		Controller.enabled = true;
		playerRotCoroutine = null;
	}

	private void EnterVehicle(LandVehicle vehicle)
	{
		currentVehicle = vehicle;
		canMove = false;
		Controller.enabled = false;
		if (recentlyDrivenVehicles.Contains(vehicle))
		{
			recentlyDrivenVehicles.Remove(vehicle);
		}
		recentlyDrivenVehicles.Insert(0, vehicle);
	}

	private void ExitVehicle(LandVehicle veh, Transform exitPoint)
	{
		currentVehicle = null;
		canMove = true;
		Controller.enabled = true;
	}

	public void Teleport(Vector3 position)
	{
		Vector3 vector = position;
		Console.Log("Player teleported: " + vector.ToString());
		if (Player.ActiveSkateboard != null)
		{
			Player.ActiveSkateboard.Equippable.Dismount();
		}
		Controller.enabled = false;
		Controller.transform.position = position;
		Controller.enabled = true;
		teleport = true;
		teleportPosition = position;
	}

	public void SetResidualVelocity(Vector3 dir, float force, float time)
	{
		residualVelocityDirection = dir.normalized;
		residualVelocityForce = force;
		residualVelocityDuration = time;
		residualVelocityTimeRemaining = time;
	}

	public void WarpToNavMesh()
	{
		if (NavMesh.SamplePosition(filter: new NavMeshQueryFilter
		{
			agentTypeID = Singleton<PlayerManager>.Instance.PlayerRecoverySurface.agentTypeID,
			areaMask = -1
		}, sourcePosition: PlayerSingleton<PlayerMovement>.Instance.transform.position, hit: out var hit, maxDistance: 100f))
		{
			PlayerSingleton<PlayerMovement>.Instance.Teleport(hit.position + Vector3.up * 1f);
			return;
		}
		Console.LogError("Failed to find recovery point!");
		PlayerSingleton<PlayerMovement>.Instance.Teleport(Vector3.up * 5f);
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
			movementEvents.Add(threshold, new MovementEvent());
		}
		movementEvents[threshold].actions.Add(action);
	}

	public void DeregisterMovementEvent(Action action)
	{
		foreach (int key in movementEvents.Keys)
		{
			MovementEvent movementEvent = movementEvents[key];
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
			MovementEvent movementEvent = movementEvents[item];
			if (Vector3.Distance(Player.Avatar.CenterPoint, movementEvent.LastUpdatedDistance) > (float)item)
			{
				movementEvent.Update(Player.Avatar.CenterPoint);
			}
		}
	}

	public void ChangeStamina(float change, bool notify = true)
	{
		if (change < 0f)
		{
			timeSinceStaminaDrain = 0f;
		}
		SetStamina(CurrentStaminaReserve + change, notify);
	}

	public void SetStamina(float value, bool notify = true)
	{
		if (CurrentStaminaReserve != value)
		{
			float currentStaminaReserve = CurrentStaminaReserve;
			CurrentStaminaReserve = Mathf.Clamp(value, 0f, StaminaReserveMax);
			if (notify && onStaminaReserveChanged != null)
			{
				onStaminaReserveChanged(CurrentStaminaReserve - currentStaminaReserve);
			}
		}
	}

	public void AddSprintBlocker(string tag)
	{
		if (!sprintBlockers.Contains(tag))
		{
			sprintBlockers.Add(tag);
		}
	}

	public void RemoveSprintBlocker(string tag)
	{
		if (sprintBlockers.Contains(tag))
		{
			sprintBlockers.Remove(tag);
		}
	}
}
