using UnityEngine;
using UnityEngine.Animations.Rigging;
using DragonWorld.AI.Utility;

namespace DragonWorld.Bird
{
    /// <summary>
    /// Main controller for the Great Bird.
    /// Acts as the Brain and Muscles, directly sending commands to the AircraftPhysics.
    /// Fully compatible with Utility AI and future player control.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AirplaneController))]
    [RequireComponent(typeof(AircraftPhysics))]
    [RequireComponent(typeof(DragonWorld.Combat.Targetable))]
    public class GreatBirdController : MonoBehaviour, IUtilityFlyer, ICombatActor, IThreatReceiver, IEnvironmentReceiver
    {
        [Header("Components")]
        public Rigidbody _rb;
        public Animator _animator;
        public AirplaneController airplaneController;
        public AircraftPhysics aircraftPhysics;
        public GreatBirdAudioController audioController;
        public DragonWorld.VFX.GreatBirdVFXController _vfxController;

        [Header("Procedural Rig & IK")]
        public OverrideTransform L_Wing_OT_Control;
        public OverrideTransform R_Wing_OT_Control;
        public TwoBoneIKConstraint L_Foot_IK;
        public TwoBoneIKConstraint R_Foot_IK;

        [Header("Procedural Animation")]
        [Tooltip("Speed multiplier for the flapping animation based on thrust")]
        public float flapAnimSpeedMultiplier = 1.5f;

        [Header("State")]
        public bool isAIControlled = true;

        [Header("Simulated Inputs")]
        [System.NonSerialized] public Vector2 input_MoveAxis;
        [System.NonSerialized] public Vector2 input_MouseAxis;
        [System.NonSerialized] public float input_rollAxis;
        [System.NonSerialized] public Vector3 mouseAim_wPos;
        [System.NonSerialized] public bool input_RMBpressed;
        [System.NonSerialized] public bool input_ShiftPressed;
        [System.NonSerialized] public bool input_JumpPressed;
        [System.NonSerialized] public bool isMovementPressed;  

        [Header("Tornado & Shock Interaction")]
        [Tooltip("Settings for chaotic wing flapping during tornado or shock.")]
        public RigControl.ShockAnimSettings shockAnim = new RigControl.ShockAnimSettings(20f, 40f, 30f, 20f, new Vector2(30f, 40f), 20f, 40f);
        [Tooltip("Frequency of the noise used for aerodynamic control surface destabilization")]
        public float aeroSurfaceNoiseFreq = 10f;
        [Tooltip("Max angle for flap control surface destabilization")]
        public float aeroSurfaceFlapAngle = 60f;
        [Tooltip("Max Z angle for flap control surface destabilization")]
        public float aeroSurfaceFlapAngleZ = 20f;
        [Tooltip("Hinge X offset for flap calculation")]
        public float aeroSurfaceHingeX = 1.0f;
        [Tooltip("Center distance for flap offset calculation")]
        public float aeroSurfaceCenterDist = 1.5f;

        [Header("Axis Inversion (Airplane Controller)")]
        [Tooltip("If the bird flies upside down or away from target, toggle these")]
        [System.NonSerialized] public bool invertPitch = true;
        [System.NonSerialized] public bool invertYaw = false;
        [System.NonSerialized] public bool invertRoll = false;

        // Internal State
        private float _currentThrustPercent = 0f;

        // Events for Rider interaction
        public event System.Action OnAttackCommandIssued;
        public event System.Action OnBirdRetreats;
        public event System.Action OnBirdMissedAttack;
        public event System.Action<GameObject> OnBirdHitAttack;
        public event System.Action OnTargetPerfectDodged;
        
        [Header("Water Interaction")]
        [Tooltip("Force applied upwards when underwater to prevent deep diving")]
        public float underwaterBuoyancyForce = 15f;
        
        private bool _isStrictlyUnderwater = false;
        private float _defaultLinearDamping = 0f;
        private float _defaultAngularDamping = 0f;

        // IUtilityFlyer Implementation
        [Header("Stamina & Boost (Bird Config)")]
        public float maxStamina = 100f;
        public float currentStamina = 100f;
        public float staminaRegenGlide = 35f; // Fast regen in glide
        public float staminaDrainFlap = 0f;   // No drain in normal flap
        public float staminaDrainBoost = 45f; // Fast drain
        public float boostThrustMultiplier = 2.5f; // Higher linear acceleration than dragon
        public bool boostEnabled = false;

        private float _animCurve = 0f;
        private int _curveHash = Animator.StringToHash("Curve");
        private bool _hasCurveParam = false;
        private float _flapTimer = 0f;
        private float _targetTalonLayerWeight = 0f;
        private float _currentTalonLayerWeight = 0f;
        
        private float _targetLegIKWeight = 0f;
        private float _currentLegIKWeight = 0f;
        private float _currentWingRollVisual = 0f;

        public float Stamina => currentStamina; 
        public float StaminaPercentage => currentStamina / Mathf.Max(1f, maxStamina);
        public float HealthPercentage 
        {
            get
            {
                if (_health != null && _health.maxHealth > 0)
                    return _health.currentHealth / _health.maxHealth;
                return 1f;
            }
        }
        public float CurrentSpeed => _rb != null ? _rb.linearVelocity.magnitude : 0f;
        
        public float RollAngle 
        {
            get 
            {
                float zAngle = transform.eulerAngles.z;
                return (zAngle > 180) ? zAngle - 360 : zAngle;
            }
        }
        
        public bool IsUnderwater => _isStrictlyUnderwater;
        public Transform RootTransform => transform;
        public Transform HeadTransform => transform; // Fallback to root for bird
        public bool IsStunned => false; // Bird doesn't have a stun state yet

        // ICombatActor Implementation
        public bool IsAttacking { get; private set; }
        public bool IsRangedAttacking { get; private set; }
        public bool HasRangedAmmo => true; // Rider throws spears, assume infinite for now
        public float RangedAmmoPercentage => 1f;
        public float RangedProjectileSpeed => 50f; // Speed of thrown spear

        [Header("Talon Strike")]
        public float telegraphDuration = 1.5f;
        public float impactDistance = 6.0f;
        public float damageAmount = 25f;
        public float impactForce = 800000f;
        public Transform leftTalon;
        public Transform rightTalon;
        [Tooltip("The layer mask representing the player dragon body for collisions")]
        public LayerMask dragonBodyMask;

        [Header("Camera Focus during Strike")]
        public bool enableStrikeCameraFocus = true;
        public float strikeCameraFocusWeight = 1f;
        public float strikeCameraFocusRadius = 1f;

        private bool _isDiving = false;
        private Coroutine _talonStrikeCoroutine;
        private string _currentSlowMoID;
        private Transform _strikeTarget;
        private Vector3 _originalLeftTalonPos;
        private Vector3 _originalRightTalonPos;
        private bool _hasOriginalTalonPos = false;
        private Vector3 _originalLWingEuler;
        private Vector3 _originalRWingEuler;
        private bool _hasOriginalWingEuler = false;
        private DragonWorld.Combat.Health _health;
        private DragonAIPilot _aiPilot;
        private Cinemachine.CinemachineImpulseSource _impulseSource;
        private DragonUtilityBrain _utilityBrain;


        private Vector3 _lastImpactPoint;
        private float _missTimer = 0f;

        private bool _tookDamageDuringAttack = false;

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken.AddListener(OnBirdDamaged);
                _health.OnDamageInfoTaken.AddListener(OnBirdDamagedWithInfo);
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken.RemoveListener(OnBirdDamaged);
                _health.OnDamageInfoTaken.RemoveListener(OnBirdDamagedWithInfo);
            }
            if (!string.IsNullOrEmpty(_currentSlowMoID) && TimeManager.Instance != null)
            {
                TimeManager.Instance.CancelSlowMo(_currentSlowMoID);
                _currentSlowMoID = null;
            }
        }

        private void OnBirdDamaged(float amount)
        {
            if (IsAttacking)
            {
                _tookDamageDuringAttack = true;
            }
        }

        private void OnBirdDamagedWithInfo(DragonWorld.Combat.DamageInfo info)
        {
            if (_vfxController != null)
            {
                _vfxController.PlayFeatherExplosion(info.HitPoint);
            }
            if (audioController != null)
            { 
                audioController.PlayHeavyDamageAudio(info, !isAIControlled); 
            }
        }

        private void Awake()
        {
            _health = GetComponent<DragonWorld.Combat.Health>();
            
            // Safe subscription in case OnEnable was called before Awake (Awake is usually first, but better safe)
            if (_health != null)
            {
                _health.OnDamageTaken.RemoveListener(OnBirdDamaged);
                _health.OnDamageTaken.AddListener(OnBirdDamaged);
                _health.OnDamageInfoTaken.RemoveListener(OnBirdDamagedWithInfo);
                _health.OnDamageInfoTaken.AddListener(OnBirdDamagedWithInfo);
            }
            _aiPilot = GetComponent<DragonAIPilot>();
            _impulseSource = GetComponent<Cinemachine.CinemachineImpulseSource>();
            _vfxController = GetComponent<DragonWorld.VFX.GreatBirdVFXController>();
            _utilityBrain = GetComponent<DragonUtilityBrain>();
            
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (airplaneController == null) airplaneController = GetComponent<AirplaneController>();
            if (aircraftPhysics == null) aircraftPhysics = GetComponent<AircraftPhysics>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (audioController == null) audioController = GetComponentInChildren<GreatBirdAudioController>();
            
            if (_animator != null)
            {
                foreach (AnimatorControllerParameter param in _animator.parameters)
                {
                    if (param.name == "Curve")
                    {
                        _hasCurveParam = true;
                        break;
                    }
                }
            }

            if (airplaneController != null)
            {
                // Disable aileron deflections because Roll is handled by physical wing rotation, just like the Dragon
                airplaneController.rollControlSensitivity = 0f;
            }
            
            if (L_Wing_OT_Control != null && R_Wing_OT_Control != null)
            {
                _originalLWingEuler = L_Wing_OT_Control.transform.localEulerAngles;
                _originalRWingEuler = R_Wing_OT_Control.transform.localEulerAngles;
                _hasOriginalWingEuler = true;
            }
        }

        private void Update()
        {
            if (_aiPilot != null && _aiPilot.enabled != isAIControlled)
                _aiPilot.enabled = isAIControlled;
                
            if (_utilityBrain != null && _utilityBrain.enabled != isAIControlled)
                _utilityBrain.enabled = isAIControlled;

            if (!isAIControlled)
            {
                ProcessPlayerInput();
            }
            
            UpdateStamina();
            HandleAnimation();
            UpdateAeroSurfaces(); 
            UpdateBurningState();
        }

        private void FixedUpdate()
        {
            ApplyFlightPhysics();
        }

        private void ProcessPlayerInput()
        {
            input_MoveAxis = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            input_ShiftPressed = Input.GetKey(KeyCode.LeftShift);
            input_JumpPressed = Input.GetButton("Jump");
            
            float roll = 0;
            if (Input.GetKey(KeyCode.Q)) roll = -1;
            if (Input.GetKey(KeyCode.E)) roll = 1;
            input_rollAxis = Mathf.Lerp(input_rollAxis, roll, Time.deltaTime * 5f);
            
            input_RMBpressed = Input.GetMouseButton(1);
            isMovementPressed = input_MoveAxis.sqrMagnitude > 0.01f || input_RMBpressed;

            boostEnabled = input_ShiftPressed && input_JumpPressed;
            if (_isStrictlyUnderwater) boostEnabled = false;

            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 2000f, ~0, QueryTriggerInteraction.Ignore);
                
                bool hitFound = false;
                float closestDist = float.MaxValue;
                Vector3 hitPoint = ray.GetPoint(1000f);
                
                foreach (var hit in hits)
                {
                    if (hit.transform.root == transform.root) continue;
                    if (hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        hitPoint = hit.point;
                        hitFound = true;
                    }
                }
                
                mouseAim_wPos = hitPoint;
            }
        }

        private void UpdateStamina()
        {
            if (_animator != null && _hasCurveParam)
            {
                _animCurve = _animator.GetFloat(_curveHash);
            }
            else
            {
                // Fallback curve simulation based on flap speed
                _flapTimer += Time.deltaTime * flapAnimSpeedMultiplier * 2f;
                _animCurve = Mathf.PingPong(_flapTimer, 1f);
            }

            bool isFlapping = input_ShiftPressed || input_JumpPressed;
            bool isBoosting = boostEnabled && currentStamina > 0;

            if (isBoosting)
            {
                currentStamina -= staminaDrainBoost * Time.deltaTime;
            }
            else if (isFlapping)
            {
                currentStamina -= staminaDrainFlap * Time.deltaTime;
            }
            else
            {
                // Gliding or resting
                currentStamina += staminaRegenGlide * Time.deltaTime;
            }

            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }

        /// <summary>
        /// Called by GreatBirdUtilityBrain to command the bird
        /// </summary>
        public void ProcessAIInput(FlyerInputData input)
        {
            if (!isAIControlled) return;

            mouseAim_wPos = input.aimPosition;
            input_ShiftPressed = input.isFlapping;
            input_JumpPressed = input.wantsToGainAltitude;
            
            // AI requests boost by flapping and aiming up/aggression, or via Action_Boost
            boostEnabled = input.isFlapping && input.wantsToGainAltitude;
            if (_isStrictlyUnderwater) boostEnabled = false;
            
            // Translate steering from new unified FlyerInputData
            input_MouseAxis = input.pitchYawInput;
            input_rollAxis = input.rollInput;
            input_MoveAxis = input.moveInput;
            input_RMBpressed = input.useSharpTurn;
            isMovementPressed = input.moveInput.sqrMagnitude > 0.01f || input.useSharpTurn;
        }

        private void ApplyFlightPhysics()
        {
            if (airplaneController == null) return;

            bool isFlapping = input_ShiftPressed || input_JumpPressed;
            bool isBoosting = boostEnabled && currentStamina > 0;
            
            // Maneuverability multiplier based on boost and curve impulse
            float maneuverMultiplier = isBoosting ? Mathf.Lerp(1.0f, 2.0f, _animCurve) : 1.0f;

            float targetPitch = 0f;
            float targetYaw = 0f;
            float targetRoll = input_rollAxis;

            if (isAIControlled)
            {
                // AI дає нам напрямок: куди він хоче дивитися (X = вліво/вправо, Y = вгору/вниз)
                float aiDesiredYaw = invertYaw ? -input_MouseAxis.x : input_MouseAxis.x;
                float aiDesiredPitch = invertPitch ? -input_MouseAxis.y : input_MouseAxis.y;

                bool isTerminal = (_aiPilot != null && _aiPilot.currentState == AIState.TerminalStrike);

                float rollMix = IsAttacking ? 0.2f : 0.8f;      // менше крену під час атаки
                float yawMix  = IsAttacking ? 1.5f : 0.2f;      // більше рульового під час атаки

                targetRoll = input_rollAxis - (aiDesiredYaw * rollMix * maneuverMultiplier);
                targetYaw  = aiDesiredYaw * yawMix * maneuverMultiplier;

                targetRoll = Mathf.Clamp(targetRoll, -1f, 1f);

                // Якщо літак/птах накренений, підйомна сила тягне його вбік і ВНИЗ. 
                // Щоб не падати і робити поворот, треба потягнути носа на себе (Pitch Up).
                float turnPitchAssist = Mathf.Abs(targetRoll) * 0.4f; // Допомога в повороті
                targetPitch = Mathf.Clamp(aiDesiredPitch * maneuverMultiplier + turnPitchAssist, -1f, 1f);
            }
            else
            {
                // Гравець: компенсація паралаксу камери
                Vector3 cameraOffset = Camera.main != null ? Camera.main.transform.position - transform.position : Vector3.zero;
                Vector3 birdTarget = mouseAim_wPos - cameraOffset;
                Vector3 pointDirection = birdTarget - transform.position;
                
                float x_angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(pointDirection, transform.right), transform.forward, transform.right);
                float y_angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(pointDirection, transform.up), transform.forward, transform.up);
                
                float rawPitchError = Mathf.Clamp(x_angle / 90f, -1f, 1f) * maneuverMultiplier;
                float rawYawError = Mathf.Clamp(-y_angle / 90f, -1f, 1f) * maneuverMultiplier; 
                
                // Damping to prevent oscillations
                float localPitchAngularVelocity = transform.InverseTransformDirection(_rb.angularVelocity).x;
                float dampingTrim = localPitchAngularVelocity * 0.5f;
                
                float totalTrim = dampingTrim;
                float manualInputFactor = 1f - Mathf.Clamp01(Mathf.Abs(rawPitchError) / 0.5f);
                totalTrim *= manualInputFactor;

                targetPitch = Mathf.Clamp(-(rawPitchError + totalTrim), -1f, 1f);
                targetYaw = rawYawError;
            }

            // Миттєве застосування Pitch, Yaw та Roll (без Lerp, щоб уникнути затримки)
            // Сигнали від AI або Гравця вже є згладженими: 
            // - AI генерує інпути пропорційно до кута помилки.
            // - Гравець має згладжування input_rollAxis у ProcessPlayerInput().
            airplaneController.Pitch = targetPitch;
            airplaneController.Yaw = targetYaw;
            airplaneController.Roll = targetRoll;

            // 2. Apply Thrust Percent
            float thrustMultiplier = isBoosting ? boostThrustMultiplier : 1.0f;
            
            if (_isStrictlyUnderwater)
            {
                // Speed boost when pointing upwards to help escape the water faster
                float upwardDot = Vector3.Dot(transform.forward, Vector3.up);
                float speedBoost = Mathf.Clamp01(upwardDot); 
                thrustMultiplier = 0.3f + (0.7f * speedBoost); 
                
                // Add buoyancy to prevent deep diving
                if (_rb != null)
                {
                    _rb.AddForce(Vector3.up * underwaterBuoyancyForce, ForceMode.Acceleration);
                }
            }

            // Pulse thrust according to the animation curve (like Dragon)
            float baseThrust = isFlapping ? Mathf.Lerp(0.2f, 1.5f, _animCurve) : (input_MoveAxis.y > 0 ? 0.3f : 0.2f);
            float targetThrust = baseThrust * thrustMultiplier;
            
            if (input_JumpPressed || input_RMBpressed) targetThrust += 0.5f;
            
            _currentThrustPercent = Mathf.Lerp(_currentThrustPercent, targetThrust, 3f * Time.fixedDeltaTime);
            airplaneController.thrustPercent = _currentThrustPercent;

            // // Лінійне прискорення при глайдінгу (компенсація втрати тяги від крил)
            // if (!isFlapping && _rb != null && CurrentSpeed < 40f)
            // {
            //     _rb.AddForce(transform.forward * 15f, ForceMode.Acceleration);
            // }
        }

        private void UpdateAeroSurfaces()
        {
            if (aircraftPhysics == null || aircraftPhysics.aerodynamicSurfaces == null) return;

            float newRollZAngle = 20f;
            
            // ФІКС 1: Жорстко ставимо 1f, як у Дракона! Аеродинаміка не повинна ламатися від візуального скейлу моделі.
            float scale = transform.localScale.x;

            // ФІКС 2: Беремо згладжений Roll з AirplaneController (який вже враховує нахил від Yaw), 
            // замість сирого і різкого input_rollAxis.
            float currentSmoothedRoll = airplaneController.Roll;
            
            // Procedural Tornado AeroSurface Destabilization
            float timeOffset = Time.time * 25f; // Flap frequency

            foreach (var surface in aircraftPhysics.aerodynamicSurfaces)
            {
                if (surface == null) continue;

                // Deactivate non-control surfaces if no forward input, exactly like the Dragon
                if (!surface.IsControlSurface)
                {
                    if (input_MoveAxis.y <= 0)
                        surface.gameObject.SetActive(false);                    
                    else
                        surface.gameObject.SetActive(true); 

                    continue;
                }

                float newLocalX;
                float localY = 0.0f * scale;
                
                float currentSpanScale = 1f;
                float rotX = 0f;
                float rotY = -90f;
                float rotZ = 0 - surface.LeftOrRight * newRollZAngle * currentSmoothedRoll;
                
                float flapLocalXOffset = 0f;
                float flapLocalYOffset = 0f;

                if (_tornadoIntensity > 0 && surface.IsControlSurface)
                {
                    float seedOffset = surface.LeftOrRight < 0 ? 0f : 100f;
                    float rollNoise = (Mathf.PerlinNoise(timeOffset + seedOffset, aeroSurfaceNoiseFreq) - 0.5f) * 2f;
                    float rollInput = Mathf.Lerp(0f, rollNoise, _tornadoIntensity);

                    float flapAngle = aeroSurfaceFlapAngle * rollInput;
                    float flapAngleZ = aeroSurfaceFlapAngleZ * rollInput;
                    
                    float hingeX = aeroSurfaceHingeX;
                    float centerDist = aeroSurfaceCenterDist;
                    float angleRad = flapAngle * Mathf.Deg2Rad;
                    flapLocalXOffset = centerDist * Mathf.Cos(angleRad) - centerDist;
                    flapLocalYOffset = centerDist * Mathf.Sin(angleRad);
                    
                    rotX = flapAngle * surface.LeftOrRight;
                    rotZ += 0 - surface.LeftOrRight * flapAngleZ;
                }

                switch (surface.InputType)
                {
                    case ControlInputType.Yaw:
                        surface.Config.chord = 3.7f * currentSpanScale * scale;
                        surface.Config.span = 2.9f * currentSpanScale * scale;
                        break;
                    case ControlInputType.Pitch:
                        surface.Config.chord = 5.3f * currentSpanScale * scale;
                        surface.Config.span = 6.0f * currentSpanScale * scale;
                        surface.transform.localPosition = new Vector3(3 * surface.LeftOrRight * scale, localY, -1.79f * scale);
                        surface.transform.localEulerAngles = new Vector3(rotX, rotY, rotZ);
                        break;
                    case ControlInputType.Roll:
                        surface.Config.chord = 2f * Mathf.Lerp(1f, 1.4f, Mathf.Abs(currentSmoothedRoll)) * currentSpanScale * scale;
                        surface.Config.span = 4 * currentSpanScale * scale;
                        newLocalX = 5.5f * surface.LeftOrRight * Mathf.Lerp(1f, 0.95f, Mathf.Abs(currentSmoothedRoll)) + flapLocalXOffset;
                        surface.transform.localPosition = new Vector3(newLocalX, localY + flapLocalYOffset, 1.38f * scale);
                        surface.transform.localEulerAngles = new Vector3(rotX, rotY, rotZ);
                        break;
                    case ControlInputType.Flap:
                        surface.Config.chord = 2.5f * currentSpanScale * scale;
                        surface.Config.span = 3.5f * currentSpanScale * scale;
                        newLocalX = 2.0f * surface.LeftOrRight * scale + flapLocalXOffset;
                        surface.transform.localPosition = new Vector3(newLocalX, localY + flapLocalYOffset, 1.38f * scale);
                        surface.transform.localEulerAngles = new Vector3(rotX, rotY, rotZ);
                        break;
                }
            }
        }

        private void HandleAnimation()
        {
            if (_animator == null) return;

            // Smoothly blend the Talons animation layer (layer 1)
            _currentTalonLayerWeight = Mathf.Lerp(_currentTalonLayerWeight, _targetTalonLayerWeight, Time.deltaTime * 5f);
            _animator.SetLayerWeight(1, _currentTalonLayerWeight);

            // Wing roll visual interpolation
            if (L_Wing_OT_Control != null && R_Wing_OT_Control != null && _hasOriginalWingEuler)
            {
                float targetWingRoll = airplaneController != null ? airplaneController.Roll * 25f : input_rollAxis * 25f;
                _currentWingRollVisual = Mathf.Lerp(_currentWingRollVisual, targetWingRoll, Time.deltaTime * 8f);
                
                // Procedural Tornado Wing Flail
                Vector3 tornadoOffsetL = Vector3.zero;
                Vector3 tornadoOffsetR = Vector3.zero;
                if (_tornadoIntensity > 0)
                {
                    float time = Time.time;
                    float freq = shockAnim.frequency;
                    float ampX = shockAnim.wingFlailAmplitudeX;
                    float ampY = shockAnim.wingFlailAmplitudeY;
                    float ampZ = shockAnim.wingFlailAmplitudeZ;
                    
                    tornadoOffsetL.x = Mathf.Sin(time * freq) * ampX * _tornadoIntensity;
                    tornadoOffsetL.y = Mathf.PerlinNoise(time * 10f, 0f) * ampY * _tornadoIntensity;
                    tornadoOffsetL.z = Mathf.Sin(time * freq * 1.2f) * ampZ * _tornadoIntensity;

                    tornadoOffsetR.x = Mathf.Sin(time * freq + Mathf.PI) * ampX * _tornadoIntensity;
                    tornadoOffsetR.y = Mathf.PerlinNoise(0f, time * 10f) * ampY * _tornadoIntensity;
                    tornadoOffsetR.z = Mathf.Sin(time * freq * 1.3f + Mathf.PI) * ampZ * _tornadoIntensity;
                }

                // Using the original Euler angles for X and Y to avoid breaking the rig orientation
                // Z axis rotation applies the roll visual
                L_Wing_OT_Control.transform.localEulerAngles = new Vector3(_originalLWingEuler.x + tornadoOffsetL.x, _originalLWingEuler.y + tornadoOffsetL.y, _originalLWingEuler.z - _currentWingRollVisual + tornadoOffsetL.z);
                R_Wing_OT_Control.transform.localEulerAngles = new Vector3(_originalRWingEuler.x + tornadoOffsetR.x, _originalRWingEuler.y + tornadoOffsetR.y, _originalRWingEuler.z - _currentWingRollVisual + tornadoOffsetR.z);
            }

            // IK smooth blending (only update if it's changing)
            if (Mathf.Abs(_currentLegIKWeight - _targetLegIKWeight) > 0.01f)
            {
                _currentLegIKWeight = Mathf.Lerp(_currentLegIKWeight, _targetLegIKWeight, Time.deltaTime * 3f);
                if (L_Foot_IK != null) L_Foot_IK.weight = _currentLegIKWeight;
                if (R_Foot_IK != null) R_Foot_IK.weight = _currentLegIKWeight;
            }
            else if (_currentLegIKWeight != _targetLegIKWeight)
            {
                _currentLegIKWeight = _targetLegIKWeight;
                if (L_Foot_IK != null) L_Foot_IK.weight = _currentLegIKWeight;
                if (R_Foot_IK != null) R_Foot_IK.weight = _currentLegIKWeight;
            }

            // *Todo: Terminal strike visual pitch adjustment (rotate the body up to expose talons)
             

            if (_isDiving)
            {
                //_animator.SetBool("IsGliding", true);
                _animator.speed = 1f;
                return;
            }

            bool isFlapping = input_ShiftPressed || input_JumpPressed;
            bool isBoosting = boostEnabled && currentStamina > 0;

            // Note: Wing Flap Audio (audioController.PlayWingFlap()) should ideally be 
            // triggered via Animation Events on the flap animation clip in the Animator.
            if (isFlapping)
            {
                _animator.SetBool("IsGliding", false);
                float speedTarget = Mathf.Max(0.5f, _currentThrustPercent * flapAnimSpeedMultiplier);
                if (isBoosting) speedTarget *= 1.35f; // Boost makes flapping faster
                _animator.speed = speedTarget;
            }
            else
            {
                _animator.SetBool("IsGliding", true);
                _animator.speed = 1f; 
            }
        }

        // ==========================================
        // ICombatActor Implementation
        // ==========================================
        
        public void ExecuteMeleeAttack(Vector3 targetPosition)
        {
            if (IsAttacking) return;
            if (IsUnderwater) return; // Hard block underwater
            
            OnAttackCommandIssued?.Invoke();
            
            // We need to find the actual target transform if possible (for slow-mo tracking)
            // Always update target for current attack
            if (_aiPilot != null && _aiPilot.target != null)
            {
                _strikeTarget = _aiPilot.target;
            }

            if (_strikeTarget != null)
            {
                if (_talonStrikeCoroutine != null) StopCoroutine(_talonStrikeCoroutine);
                _talonStrikeCoroutine = StartCoroutine(TalonStrikeSequence());
            }
            else
            {
                // Fallback
                IsAttacking = true;
                Invoke(nameof(ResetMeleeAttack), 1.5f);
            }
        }

        private void ResetMeleeAttack()
        {
            IsAttacking = false;
            _isDiving = false;
            _targetTalonLayerWeight = 0f; // Smoothly close talons
            _targetLegIKWeight = 0f; // Disable IK for legs
            if (_animator != null) _animator.SetBool("isDiving", false);

            if (_vfxController != null)
            {
                _vfxController.StopStrikeSignal();
                _vfxController.StopTalonTrails();
            }
 
            // ФІКС: записуємо час кінця атаки, а не початку
            int flyerID = gameObject.GetInstanceID();
            CooldownConsideration.RecordExecution(flyerID, "TalonStrike");

        }

        private System.Collections.IEnumerator TalonStrikeSequence()
        {
            IsAttacking = true;
            _isDiving = false; // Start with active flapping to catch up

            _tookDamageDuringAttack = false;

            if (_vfxController != null) _vfxController.PlayImplosionVacuum();
            if (audioController != null) audioController.PlayImplosionVacuum();

            // Імітуємо затримку реакції птаха на команду вершника
            yield return new WaitForSeconds(0.4f);

            if (CameraEffectsManager.Instance != null && _strikeTarget != null)
            {
                CameraEffectsManager.Instance.PlayVertigoEffect(null, 30f, telegraphDuration + 0.5f);
            }

            if (_vfxController != null) _vfxController.PlayStrikeSignal();

            // ФІКС: негайно переключаємо на підхід до цілі
            if (_aiPilot != null) _aiPilot.currentState = AIState.TerminalStrike;
            
            // Warning screech at the very beginning of the attack decision
            if (audioController != null) audioController.PlayScreechAt(transform.position);
            if (audioController != null) audioController.PlayIgnitionSignal();
            
            if (!_hasOriginalTalonPos && leftTalon != null && rightTalon != null)
            {
                _originalLeftTalonPos = leftTalon.localPosition;
                _originalRightTalonPos = rightTalon.localPosition;
                _hasOriginalTalonPos = true;
            }

            DragonWorld.AI.Utility.IThreatReceiver targetDragon = _strikeTarget != null ? _strikeTarget.GetComponentInParent<DragonWorld.AI.Utility.IThreatReceiver>() : null;
            Rigidbody targetRb = _strikeTarget != null ? _strikeTarget.GetComponentInParent<Rigidbody>() : null;
            GameObject focusPoint = null; 

            bool telegraphStarted = false;
            bool terminalStarted = false;
            bool impactOccurred = false;
            bool dodged = false;
            bool interrupted = false;
            
            float attackTimer = 0f;

            try
            {
                // Infinite Approach Loop - Bird will chase UNTIL it hits or is interrupted
                while (true)
                {
                    if (_strikeTarget == null) break;
                    
                    attackTimer += Time.deltaTime;
                    if (attackTimer > 8.0f) // Timeout to prevent infinite looping
                    {
                        break;
                    }

                    float distance = Vector3.Distance(transform.position, _strikeTarget.position);
                    
                    //Vector3 exactHitboxTarget = _aiPilot != null ? _aiPilot.GetBestMeleeTarget(_strikeTarget) : _strikeTarget.position;
                    DragonHitbox exactHitbox = _aiPilot != null ? _aiPilot.SelectBestStrikeHitbox(_strikeTarget) : null; 
                    Vector3 exactHitboxTarget = exactHitbox != null ? exactHitbox.transform.position : _strikeTarget.position;
                    float hitboxDistance = Vector3.Distance(transform.position, exactHitboxTarget);
                    
                    _lastImpactPoint = exactHitboxTarget;

                    // Calculate time to impact
                    float timeToImpact = 999f;
                    if (targetRb != null && _rb != null)
                    {
                        Vector3 relativeVel = _rb.linearVelocity - targetRb.linearVelocity;
                        float closingSpeed = Vector3.Dot(relativeVel, (_strikeTarget.position - transform.position).normalized);
                        if (closingSpeed > 0) timeToImpact = distance / closingSpeed;
                    }
                    else if (_rb != null)
                    {
                        float closingSpeed = Vector3.Dot(_rb.linearVelocity, (_strikeTarget.position - transform.position).normalized);
                        if (closingSpeed > 0) timeToImpact = distance / closingSpeed;
                    }

                    // 1. Telegraph Phase (SlowMo, Sparks, Threat)
                    if (!telegraphStarted && (timeToImpact <= telegraphDuration || distance <= 35f))
                    {
                        telegraphStarted = true;
                        
                        if (audioController != null) audioController.PlayTalonStrikeAt(transform.position);

                        if (targetDragon != null)
                        {
                            targetDragon.SetUnderThreat(telegraphDuration);
                            
                            if (enableStrikeCameraFocus)
                            {
                                focusPoint = new GameObject("BirdStrike_POI");
                                focusPoint.transform.position = transform.position;
                                focusPoint.transform.SetParent(transform);
                                targetDragon.AddTemporaryTarget(focusPoint.transform, telegraphDuration + 1f, strikeCameraFocusWeight, strikeCameraFocusRadius);
                            }
                        }

                        if (_animator != null)
                        {
                            _targetTalonLayerWeight = 1f; // Smoothly open talons
                            _animator.CrossFade("Talons_Open", 0.2f, 1);
                        }
                    }

                    // 2. Terminal Phase (Glide, Aggressive Homing)
                    if (telegraphStarted && !terminalStarted && (timeToImpact <= 0.5f || distance <= 15f))
                    {
                        terminalStarted = true;
                        _isDiving = true; // Fold wings and glide in the final moment
                        if (_animator != null) _animator.SetBool("isDiving", true);
                        if (_animator != null) _animator.SetBool("isGliding", false);
                        if (_aiPilot != null) _aiPilot.currentState = AIState.TerminalStrike;
                        _targetLegIKWeight = 1f; // Enable IK for legs during strike

                        _currentSlowMoID = "BirdStrike_" + GetInstanceID();
                        if (TimeManager.Instance != null)
                        {
                            TimeManager.Instance.RequestSlowMo(_currentSlowMoID, 0.3f, telegraphDuration, 40, 0f, 0.2f);
                        }
                        
                        if (_vfxController != null)
                        {
                            _vfxController.PlayTalonTrails();
                        }
                    }

                    // 3. Impact Detection & Miss Detection
                    float leftTalonDist  = leftTalon  != null ? Vector3.Distance(leftTalon.position,  exactHitboxTarget) : float.MaxValue;
                    float rightTalonDist = rightTalon != null ? Vector3.Distance(rightTalon.position, exactHitboxTarget) : float.MaxValue;
                    float talonDist = Mathf.Min(leftTalonDist, rightTalonDist);

                    if (talonDist < 2.5f || hitboxDistance < 3.5f)
                    {
                        //_lastImpactPoint = talonDist < leftTalonDist ? rightTalon.position : leftTalon.position;
                        impactOccurred = true;
                        break;
                    }
                    else if (terminalStarted && distance > 10f)
                    {
                        // Check if we flew past the target
                        float dotProduct = Vector3.Dot((_strikeTarget.position - transform.position).normalized, transform.forward);
                        _missTimer += Time.deltaTime;
                        if (dotProduct < -0.3f && _missTimer > 0.3f) // стабільно летить від цілі >0.3 сек
                        {
                            break; // просто break, без dodged=true
                        }
                        else if (dotProduct >= 0f)
                        {
                            _missTimer = 0f; // скидаємо якщо знову дивиться на ціль
                        }
                        // if (dotProduct < 0f)
                        // { 
                        //     Debug.Log("GreatBirdController: Strike missed. Distance to target: " + distance.ToString("F2") + "m, Hitbox distance: " + hitboxDistance.ToString("F2") + "m");
                        //     break;
                        // }
                    }

                    // Flight Controls: Force Flap & Boost if NOT in Terminal Phase
                    if (!terminalStarted)
                    {
                        // Note: If isAIControlled is false, we don't want to override manual inputs here.
                        // But since this is an AI behavior, we assume it's AI controlled or we just force it for the attack sequence.
                        input_ShiftPressed = true; // Force flapping
                        if (currentStamina > 0)
                        {
                            boostEnabled = true;
                            input_JumpPressed = true; // Force boost
                        }
                    }
                    
                    // if (!terminalStarted)
                    // {
                    //     input_ShiftPressed = true;
                    //     boostEnabled = false; // Без бусту під час заходу — точніша траєкторія
                    //     input_JumpPressed = false;
                    // }

                    // Procedural IK logic for talons during Telegraph
                    if (telegraphStarted && leftTalon != null && rightTalon != null)
                    {
                        if (terminalStarted && distance < 12f)
                        {
                            // Final stretch: connect talons towards the exact target hitbox
                            Vector3 leftTarget = exactHitboxTarget - transform.right * 0.8f;
                            Vector3 rightTarget = exactHitboxTarget + transform.right * 0.8f;

                            leftTalon.position = Vector3.Lerp(leftTalon.position, leftTarget, Time.deltaTime * 15f);
                            rightTalon.position = Vector3.Lerp(rightTalon.position, rightTarget, Time.deltaTime * 15f);
                        }
                        else
                        {
                            // Telegraph preparation: raise and open talons with some noise
                            float noiseL = Mathf.PerlinNoise(Time.time * 15f, 0f) * 0.5f;
                            float noiseR = Mathf.PerlinNoise(0f, Time.time * 15f) * 0.5f;
                            
                            Vector3 extendL = new Vector3(0, -1f - noiseL, 1.5f + noiseL);
                            Vector3 extendR = new Vector3(0, -1f - noiseR, 1.5f + noiseR);
                            
                            leftTalon.localPosition = Vector3.Lerp(leftTalon.localPosition, _originalLeftTalonPos + extendL, Time.deltaTime * 8f);
                            rightTalon.localPosition = Vector3.Lerp(rightTalon.localPosition, _originalRightTalonPos + extendR, Time.deltaTime * 8f);
                        }
                    }

                    if (targetDragon != null && targetDragon.IsPerfectDodgeWindow)
                    {
                        if (!dodged)
                        {
                            OnTargetPerfectDodged?.Invoke();
                        }
                        dodged = true; // Latch dodge success
                    }

                    // Interruption check: if we took any damage during the attack sequence
                    if (_tookDamageDuringAttack)
                    {
                        interrupted = true;
                        OnBirdRetreats?.Invoke();
                        if (targetDragon != null)
                        {
                            DragonWorld.Economy.EconomyEvents.OnDiscreteActionTriggered?.Invoke(targetDragon.GameObject, DragonWorld.Economy.ScoreEventType.StormRetribution, transform.position);
                        } 
                        break; 
                    }

                    yield return null;
                }

                // Execute Impact if not interrupted
                if (impactOccurred && !interrupted)
                {
                    if (_animator != null) _animator.CrossFade("Talons_Close", 0.1f, 1);
                    if (_vfxController != null) _vfxController.StopTalonTrails();

                    if (dodged)
                    {
                        // Miss
                        if (targetDragon != null)
                        {
                            targetDragon.TriggerPerfectDodge();
                            DragonWorld.Economy.EconomyEvents.OnDiscreteActionTriggered?.Invoke(targetDragon.GameObject, DragonWorld.Economy.ScoreEventType.BirdTalonStrike_Miss, transform.position);
                        }
                        OnBirdMissedAttack?.Invoke();
                        // if (audioController != null) audioController.PlayScreechAt(transform.position); // Disappointment // The same screech is not ok
                    }
                    else
                    {
                        // Hit
                        if (targetDragon != null)
                        {
                            DragonWorld.Economy.EconomyEvents.OnDiscreteActionTriggered?.Invoke(targetDragon.GameObject, DragonWorld.Economy.ScoreEventType.BirdTalonStrike_Hit, transform.position);
                            targetDragon.ApplyShockState(0.8f);
                        }

                        DragonWorld.Combat.Health health = null;
                        if (_strikeTarget != null) health = _strikeTarget.GetComponentInParent<DragonWorld.Combat.Health>();
                        
                        if (health != null && _strikeTarget != null)
                        {
                            DragonWorld.Combat.DamageInfo dInfo = new DragonWorld.Combat.DamageInfo(
                                damageAmount, 
                                (_strikeTarget.position - transform.position).normalized + Vector3.down * 0.5f,
                                impactForce, 
                                _strikeTarget.position
                            );
                            health.TakeDamage(dInfo);
                            OnBirdHitAttack?.Invoke(health.gameObject);
                        }

                        if (audioController != null) 
                        {
                            audioController.PlayTalonStrikeAt(transform.position);
                            audioController.PlayTriumphantCawAt(transform.position);
                            audioController.ImpactLightning(true); // Plays a heavy thunder/discharge sound
                        }

                        if (_vfxController != null)
                        {
                            //Vector3 impactPos = _strikeTarget != null ? _strikeTarget.position : transform.position;
                            _vfxController.PlayTalonImpactVFX(_lastImpactPoint);
                        }
                        
                        // Create physical light flash
                        GameObject flashGO = new GameObject("BirdStrikeFlash");
                        flashGO.transform.position = transform.position;
                        Light flashLight = flashGO.AddComponent<Light>();
                        flashLight.type = LightType.Point;
                        flashLight.range = 80f;
                        flashLight.intensity = 2000000f; // HDRP Lumen intensity
                        flashLight.color = new Color(1f, 0.9f, 0.8f);
                        Destroy(flashGO, 0.15f);
                        
                        if (_impulseSource != null)
                        {
                            Vector3 impulseDirection = (_strikeTarget.position - transform.position).normalized;
                            _impulseSource.GenerateImpulseAt(transform.position, impulseDirection * 2f);
                        }

                        if (CameraEffectsManager.Instance != null)
                        {
                            CameraEffectsManager.Instance.PlayPunchZoom(15f, 6f, 0.15f, true);
                        }
                    }
                }

                // Phase 4: Recovery
                if (leftTalon != null && rightTalon != null)
                {
                    float recTimer = 0f;
                    while (recTimer < 1f)
                    {
                        recTimer += Time.deltaTime;
                        leftTalon.localPosition = Vector3.Lerp(leftTalon.localPosition, _originalLeftTalonPos, Time.deltaTime * 5f);
                        rightTalon.localPosition = Vector3.Lerp(rightTalon.localPosition, _originalRightTalonPos, Time.deltaTime * 5f);
                        yield return null;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(1f);
                }
            }
            finally
            {
                // Cleanup SlowMo if it was started and not cancelled
                if (telegraphStarted && TimeManager.Instance != null && !string.IsNullOrEmpty(_currentSlowMoID))
                {
                    TimeManager.Instance.CancelSlowMo(_currentSlowMoID, 0.2f);
                    _currentSlowMoID = null;
                }

                if (focusPoint != null) Destroy(focusPoint);

                ResetMeleeAttack();
            }
        }

        public void ExecuteRangedAttack(Vector3 targetPosition, bool isFiring)
        {
            IsRangedAttacking = isFiring;
            
            if (isFiring)
            {
                Debug.Log("Rider throws spear at " + targetPosition);
                // TODO: RiderAudioController.PlayRiderAttackShout() or similar
                // According to architecture, Rider handles its own audio
            }
        }

        // ==========================================
        // IThreatReceiver Implementation
        // ==========================================

        GameObject IThreatReceiver.GameObject => gameObject;
        bool IThreatReceiver.IsPerfectDodgeWindow => false; // Bird doesn't have dodge window yet
        bool IThreatReceiver.IsAIControlled => isAIControlled;
        bool IThreatReceiver.IsFireBreathing => false; // Bird doesn't breathe fire

        public void SetUnderThreat(float duration)
        {
            // Future implementation for bird
        }

        public void TriggerPerfectDodge()
        {
            // Future implementation
        }

        private Coroutine _shockStateCoroutine;

        public void ApplyShockState(float intensity)
        {
            if (IsStunned || IsUnderwater) return;

            DragonWorld.Economy.EconomyEvents.OnDiscreteActionTriggered?.Invoke(gameObject, DragonWorld.Economy.ScoreEventType.ShockPenalty, transform.position);

            if (_shockStateCoroutine != null) StopCoroutine(_shockStateCoroutine);
            _shockStateCoroutine = StartCoroutine(ShockStateRoutine(intensity));
        }

        private System.Collections.IEnumerator ShockStateRoutine(float intensityMultiplier)
        {
            if (_rb != null)
            {
                _rb.AddTorque(UnityEngine.Random.insideUnitSphere * (300000f * intensityMultiplier), ForceMode.Impulse);
            }
            
            // Abort ongoing attacks
            ResetMeleeAttack();
            
            // Trigger visual wing chaos through tornado intensity equivalent
            float originalTornado = _tornadoIntensity;
            _tornadoIntensity = Mathf.Max(_tornadoIntensity, 1f * intensityMultiplier);
            
            float duration = 2f * intensityMultiplier;
            float timer = 0f;
            
            // if (hud != null) hud.FlashWhiteOut(0.2f); // Assuming bird could have HUD access, or omit if none
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                // Decay the effect
                _tornadoIntensity = Mathf.Lerp(1f * intensityMultiplier, originalTornado, progress);
                
                yield return null;
            }
            
            _tornadoIntensity = originalTornado;
            _shockStateCoroutine = null;
        }

        // ==========================================
        // Burning Implementation
        // ==========================================
        
        public class BurningZone
        {
            public float Timer;
            public Transform HitTransform;
            public Vector3 LocalHitOffset;
            public GameObject Attacker;
            public float ScoreTickTimer;
        }

        private System.Collections.Generic.Dictionary<Collider, BurningZone> _activeBurningZones = new System.Collections.Generic.Dictionary<Collider, BurningZone>();
        private bool _hitByFireThisFrame = false;

        public void StartBurning(float addedDuration, Collider hitCollider, Vector3 hitPosition, float maxDuration = 8f, float initialDuration = 1.5f, GameObject attacker = null)
        {
            if (hitCollider == null) return;

            _hitByFireThisFrame = true;

            if (_activeBurningZones.TryGetValue(hitCollider, out BurningZone zone))
            {
                zone.Timer = Mathf.Clamp(zone.Timer + addedDuration, 0f, maxDuration);
                zone.Attacker = attacker;
                if (zone.HitTransform != null)
                {
                    zone.LocalHitOffset = zone.HitTransform.InverseTransformPoint(hitPosition);
                }
            }
            else
            {
                BurningZone newZone = new BurningZone();
                newZone.Timer = initialDuration;
                newZone.HitTransform = hitCollider.transform;
                newZone.LocalHitOffset = hitCollider.transform.InverseTransformPoint(hitPosition);
                newZone.Attacker = attacker;
                newZone.ScoreTickTimer = 0f;

                if (_activeBurningZones.Count == 0 && _vfxController != null)
                {
                    _vfxController.PlayBurnVFX(hitPosition);
                }

                _activeBurningZones[hitCollider] = newZone;
            }

            if (_vfxController != null)
            {
                _vfxController.PlayFeatherExplosion(hitPosition);
            }
        }

        public bool IsBurning()
        {
            return _activeBurningZones.Count > 0;
        }

        private void UpdateBurningState()
        {
            if (_activeBurningZones.Count > 0)
            {
                // Apply Damage Over Time
                if (_health != null)
                {
                    GameObject mainAttacker = null;
                    foreach (var zone in _activeBurningZones.Values)
                    {
                        if (zone.Attacker != null)
                        {
                            mainAttacker = zone.Attacker;
                            break;
                        }
                    }
                    _health.TakeContinuousDamage(20f * Time.deltaTime, mainAttacker);
                }

                System.Collections.Generic.List<Collider> keysToRemove = new System.Collections.Generic.List<Collider>();

                foreach (var kvp in _activeBurningZones)
                {
                    Collider col = kvp.Key;
                    BurningZone zone = kvp.Value;

                    // Score Tick
                    if (zone.Attacker != null)
                    {
                        zone.ScoreTickTimer += Time.deltaTime;
                        if (zone.ScoreTickTimer >= 1.0f)
                        {
                            zone.ScoreTickTimer -= 1.0f;
                            DragonWorld.Economy.EconomyEvents.OnDiscreteActionTriggered?.Invoke(zone.Attacker, DragonWorld.Economy.ScoreEventType.FireBreathTick, zone.HitTransform != null ? zone.HitTransform.position : transform.position);
                        }
                    }

                    if (!_hitByFireThisFrame)
                    {
                        zone.Timer -= Time.deltaTime;
                    }

                    if (zone.Timer <= 0)
                    {
                        keysToRemove.Add(col);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _activeBurningZones.Remove(key);
                }
                
                if (_activeBurningZones.Count == 0)
                {
                    if (_vfxController != null) _vfxController.StopBurnVFX();
                    DragonWorld.Economy.EconomyEvents.OnDiscreteActionTriggered?.Invoke(gameObject, DragonWorld.Economy.ScoreEventType.FireExtinguished, transform.position);
                }
            }
            
            _hitByFireThisFrame = false;
        }

        public void ModifyStamina(float amount)
        {
            currentStamina = Mathf.Clamp(currentStamina + amount, 0, maxStamina);
        }

        public void AddTemporaryTarget(Transform target, float duration, float weight, float radius)
        {
            // Future implementation if bird needs to focus camera on threat
        }

        public void RemoveTemporaryTarget(Transform target)
        {
            // Future implementation
        }

        // ==========================================
        // IEnvironmentReceiver Implementation
        // ==========================================

        bool IEnvironmentReceiver.IsUnderWater => IsUnderwater;

        void IEnvironmentReceiver.SetUnderwaterState(bool state)
        {
            if (_isStrictlyUnderwater == state) return;
            
            _isStrictlyUnderwater = state;

            if (state)
            {
                // Enter Water
                if (_rb != null)
                {
                    _defaultLinearDamping = _rb.linearDamping;
                    _defaultAngularDamping = _rb.angularDamping;
                    _rb.linearDamping = 3f;
                    _rb.angularDamping = 7f;
                }

                if (audioController != null)
                {
                    audioController.MuteAmbientForUnderwater(isAIControlled);
                    audioController.PlayUnderwaterSplashAt(transform.position);
                }

                // Extinguish fire if burning
                _activeBurningZones.Clear();
                if (_vfxController != null) _vfxController.StopBurnVFX();
            }
            else
            {
                // Exit Water
                if (_rb != null)
                {
                    _rb.linearDamping = _defaultLinearDamping;
                    _rb.angularDamping = _defaultAngularDamping;
                }

                if (audioController != null)
                {
                    audioController.RestoreAmbientAfterUnderwater(isAIControlled);
                    audioController.PlayWaterSplashAt(transform.position);
                }
            }
        }

        void IEnvironmentReceiver.PlayWaterDrops()
        {
            if (_vfxController != null)
            {
                _vfxController.PlayWaterDrops(); 
            }
        }

        private float _tornadoIntensity = 0f;

        public void ApplyTornadoBuffeting(float intensity)
        {
            _tornadoIntensity = intensity;
            // Forces (Suction, Lift, Torque, Wobble) are now applied directly by the TornadoController,
            // just like for the Dragon. We only store the intensity for animation and aero-surface destabilization.
        }

        public void AddExternalWind(Vector3 velocity, float turbulence)
        {
            // Future implementation: Will affect physics naturally if we add forces, or just visual
            // if (_vfxController != null) _vfxController.UpdateExternalForces(velocity, turbulence);
        }

        public void PlayStruggleVocalization()
        {
            if (audioController != null)
            {
                audioController.PlayScreechAt(transform.position);
            }
        }

        public void SetContinuousWaterDrops(float rate)
        {
            if (_vfxController != null) _vfxController.SetContinuousWaterDrops(rate);
        }

        // ==========================================
        // VFX Wrappers
        // ==========================================

        public void PlayBurnVFX(Vector3 hitPosition)
        {
            if (_vfxController != null)
            {
                _vfxController.PlayBurnVFX(hitPosition);
            }
        }

        public void StopBurnVFX()
        {
            if (_vfxController != null)
            {
                _vfxController.StopBurnVFX();
            }
        }
            
    }
}
