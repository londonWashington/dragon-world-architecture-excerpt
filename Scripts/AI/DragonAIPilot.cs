using UnityEngine;
using DragonWorld.AI.Utility;

 
public class DragonAIPilot : MonoBehaviour
{
    [Header("References")]
    public IUtilityFlyer flyer;
    public ICombatActor combatActor;
    public DragonWaterInteraction waterInteraction;
    public DragonUtilityBrain utilityBrain;
    public Transform target; // The primary target (usually the player)

    [Header("Behavior Settings")]
    public AIState currentState = AIState.Chase;
    public float targetAltitude = 30f;    // AI will try to maintain this height above ground
    public float safeDistance = 150f;     // Exit distance for Flee mode
    public float ramDistance = 15f;       // Distance at which AI tries to impact the target
    public float repositionDistance = 80f; // Distance to fly away during Boom and Zoom

    [Header("Flight Parameters")]
    public float lookAheadDistance = 50f; // Lookahead distance for steering calculation
    public float avoidDistance = 45f;     // Distance for obstacle detection sensors
    public float avoidForce = 3.5f;       // Strength of repulsion from obstacles (Increased)
    public LayerMask obstacleMask;        // Layers considered as obstacles (walls, mountains)
    public LayerMask groundMask;          // Layer for altitude detection
    
    [Header("Water Settings")]
    public float waterAvoidanceDistance = 15f;
    public float waterAvoidanceForce = 6f;

    [Header("Wingman Settings")]
    public Vector3 wingmanOffset = new Vector3(20, 5, -10); // Desired position relative to target
    public float positionSwitchInterval = 10f; // How often to swap sides (left/right)
    public float overtakeInterval = 15f;       // How often to attempt moving ahead of the target

    [Header("Debug")]
    public bool showGizmos = true;
    public float currentDangerLevel; // Exposed for Utility AI

    private float _positionSwitchTimer;
    private float _overtakeTimer;
    private bool _isOvertaking;
    private Vector3 _currentWingmanOffset;
    private Vector3 _predictedTargetPos;

    // Cached target Rigidbody
    private Rigidbody _targetRb;
    private Transform _lastTarget;
    private DragonHitbox[] _targetHitboxes;

    // Cached own components
    private Rigidbody _myRb;

    // Brain State Variables
    private float _stuckTimer;
    private bool _isRecovering;
    private float _recoveryTimer;
    private float _strafeDirection;
    
    // Brain Input Smoothing (simulates physical reaction limits)
    private Vector2 _smoothedPitchYaw;
    private float _smoothedRoll;
    
    // Cached variables
    // Removed _cachedFireHitbox as it's now handled by ICombatActor

    void Awake()
    {
        flyer = GetComponent<IUtilityFlyer>();
        combatActor = GetComponent<ICombatActor>();
        _myRb = GetComponent<Rigidbody>();
        if (!waterInteraction) waterInteraction = GetComponent<DragonWaterInteraction>();
        if (!waterInteraction) waterInteraction = GetComponentInChildren<DragonWaterInteraction>();
        if (!utilityBrain) utilityBrain = GetComponent<DragonUtilityBrain>();
        
        _currentWingmanOffset = wingmanOffset;
        _strafeDirection = Random.value > 0.5f ? 1f : -1f;
    }

    void Start()
    {
    }

    void Update()
    {
        if (flyer == null) return;

        if (target != null && target != _lastTarget)
        {
            _targetRb = target.GetComponent<Rigidbody>();
            _targetHitboxes = target.GetComponentsInChildren<DragonHitbox>();
            _lastTarget = target;
        }

        if (flyer.IsStunned)
        {
            flyer.ProcessAIInput(new FlyerInputData());
            return;
        }

        UpdateAIState();
        
        // 2. Predict target movement for head gazing and fire aiming
        _predictedTargetPos = GetLeadTargetPosition(/* currentState == AIState.Intercept ? 1.5f :  */0.5f);
        
        // 1. Determine steering direction based on active tactics
        Vector3 steering = CalculateSteering();
        
        
        Vector3 aimPoint;
        if (combatActor != null && combatActor.IsRangedAttacking)
        {
            aimPoint = GetFireLeadPosition();
        }
        else if (combatActor != null && combatActor.IsAttacking)
        {
            aimPoint = target != null ? target.position + Vector3.up * 2f : transform.position + transform.forward * 10f;
        }
        else
        {
            aimPoint = transform.position + steering.normalized * lookAheadDistance;
        }

       // Update target aim point smoothly
        Vector3 finalAimPos = aimPoint;
        if (utilityBrain != null && utilityBrain.Context != null)
        {
            AIDataContext ctx = utilityBrain.Context;
            // Lerp the previous aim position (тільки якщо вона вже була встановлена)
            if (ctx.currentInputData.aimPosition != Vector3.zero)
            {
                finalAimPos = Vector3.Lerp(ctx.currentInputData.aimPosition, aimPoint, Time.deltaTime * 5f);
            }
        }
        
        // 3. Brain: Collision Detection & Recovery
        CheckStuckStatus();

        // Prepare local steering structure
        // Prepare local steering structure (Навігація Пілота)
        FlyerInputData newInputs = new FlyerInputData();
        newInputs.aimPosition = finalAimPos;

        // 4. Translate steering into physical inputs
        if (flyer.IsUnderwater)
            PerformUnderwaterBehavior(ref newInputs);
        else if (_isRecovering)
            PerformRecovery(ref newInputs);
        else
            ConvertSteeringToInputs(steering, ref newInputs);
        
        // 5. Apply AI Utility Brain decisions (Злиття з Мозком без Рефлексії)
        if (utilityBrain != null && utilityBrain.Context != null)
        {
            AIDataContext ctx = utilityBrain.Context;

            // Зливаємо рішення Пілота (навігація) з рішеннями Мозку (вміння)
            if (ctx.currentInputData.isGliding)
            {
                newInputs.isFlapping = false;
                newInputs.isGliding = true;
            }
            else
            {
                newInputs.isFlapping = newInputs.isFlapping || ctx.currentInputData.isFlapping;
                newInputs.isGliding = false;
            }

            newInputs.wantsToGainAltitude = newInputs.wantsToGainAltitude || ctx.currentInputData.wantsToGainAltitude;
            newInputs.useSharpTurn = newInputs.useSharpTurn || ctx.currentInputData.useSharpTurn;
            
            // Рух вперед: беремо максимум між потребою навігатора і мозку
            newInputs.moveInput = new Vector2(
                newInputs.moveInput.x,
                Mathf.Max(newInputs.moveInput.y, ctx.currentInputData.moveInput.y)
            );
        }

        // ВІДПРАВЛЯЄМО ФІНАЛЬНИЙ ПАКЕТ У ТІЛО
        flyer.ProcessAIInput(newInputs);
    }

    void CheckStuckStatus()
    {
        float speed = flyer.CurrentSpeed;
        
        // We assume we're always trying to move forward
        bool isTryingMove = true;
        if (!_isRecovering && isTryingMove && speed < 1.5f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 1.2f)
            {
                _isRecovering = true;
                _recoveryTimer = 2.0f; // Recover for 2 seconds
                _stuckTimer = 0;
                _strafeDirection = Random.value > 0.5f ? 1f : -1f;
            }
        }
        else
        {
            _stuckTimer = Mathf.Max(0, _stuckTimer - Time.deltaTime);
        }

        if (_isRecovering)
        {
            _recoveryTimer -= Time.deltaTime;
            // If underwater, let the underwater logic take priority
            if (_recoveryTimer <= 0 || flyer.IsUnderwater) _isRecovering = false;
        }
    }

    void PerformRecovery(ref FlyerInputData inputs)
    {
        // Recovery Inputs (Move Back, Up, and Strafe)
        inputs.moveInput = new Vector2(_strafeDirection, -1f); // Back and side
        inputs.wantsToGainAltitude = true; // Fly Up
        inputs.isFlapping = true; // Flap wings
        
        // Look away from the wall (simplified: look up)
        inputs.aimPosition = transform.position + Vector3.up * 50f - transform.forward * 10f;
    }

    void PerformUnderwaterBehavior(ref FlyerInputData inputs)
    {
        // 1. Get ideal direction for underwater (chase, surface, avoid obstacles)
        Vector3 steerDir = CalculateUnderwaterSteering();

        // 2. Look at target for visuals
        inputs.aimPosition = transform.position + steerDir * 50f;

        // 3. Convert steerDir to Pitch and Yaw using local angles to prevent spinning
        Vector3 toTarget = steerDir;
        
        // Project onto right and up vectors to find signed angles
        float pitchAngle = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(toTarget, transform.right), transform.right);
        float yawAngle = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(toTarget, transform.up), transform.up);

        // Map angles to [-1, 1] inputs. e.g. 45 degrees = full input
        // Negative pitchAngle means target is ABOVE, which requires Nose UP (positive input)
        float maxAngle = 45f;
        float pitchInput = Mathf.Clamp(-pitchAngle / maxAngle, -1f, 1f);
        float yawInput = Mathf.Clamp(yawAngle / maxAngle, -1f, 1f);

        inputs.pitchYawInput = new Vector2(yawInput, pitchInput);
        
        // 4. Movement inputs (Swim Forward)
        inputs.moveInput = new Vector2(0f, 1f); 
        
        // Use Jump to generate lift ONLY if we really want to go up
        inputs.wantsToGainAltitude = steerDir.y > 0.3f;
    }

    Vector3 CalculateUnderwaterSteering()
    {
        Vector3 desiredDir = Vector3.up; // Default to surfacing

        bool targetIsValid = target != null;
        if (targetIsValid)
        {
            float distToTarget = Vector3.Distance(transform.position, target.position);
            
            // Determine if target is underwater
            bool targetIsUnderwater = false;
            if (waterInteraction != null && waterInteraction.waterSurface != null)
            {
                targetIsUnderwater = target.position.y < waterInteraction.CurrentBodySurfacePosition.y;
            }
            else
            {
                targetIsUnderwater = target.position.y < 0f;
            }
            
            // Decide whether to chase or surface
            if (targetIsUnderwater || distToTarget < 50f)
            {
                // Chase target
                Vector3 chasePos = target.position;
                if (distToTarget < ramDistance)
                {
                    chasePos += (target.position - transform.position).normalized * 5f;
                }
                desiredDir = (chasePos - transform.position).normalized;
            }
            else
            {
                // Target is far and above water, switch to surface
                desiredDir = Vector3.up;
                // Add horizontal bias towards target
                Vector3 toTargetHoriz = (target.position - transform.position);
                toTargetHoriz.y = 0;
                desiredDir = (desiredDir * 2f + toTargetHoriz.normalized).normalized;
            }
        }

        // Obstacle Avoidance (Seabed, Rocks)
        Vector3 avoidance = Vector3.zero;
        
        Vector3 origin = flyer.HeadTransform.position;

        // Rays covering front hemisphere
        Vector3[] rays = new Vector3[] {
            transform.forward,
            (transform.forward - transform.right).normalized,
            (transform.forward + transform.right).normalized,
            (transform.forward + transform.up).normalized,
            (transform.forward - transform.up).normalized
        };

        foreach (var dir in rays)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, avoidDistance * 1.5f, obstacleMask | groundMask))
            {
                avoidance += hit.normal * avoidForce * (1f - (hit.distance / (avoidDistance * 1.5f)));
            }
        }

        // Keep away from seabed (Altitude Control reversed for underwater)
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit, 50f, groundMask))
        {
            if (groundHit.distance < targetAltitude)
            {
                float strength = 1f - (groundHit.distance / targetAltitude);
                avoidance += Vector3.up * strength * 5.0f; // Push UP strongly
            }
        }

        return (desiredDir + avoidance).normalized;
    }

    void UpdateAIState()
    {
        if (target == null) return;

        // Periodic wingman behavior: alternating between flying behind and overtaking
        if (currentState == AIState.Wingman)
        {
            // _positionSwitchTimer += Time.deltaTime; // Temporarily disabled for debugging
            // _overtakeTimer += Time.deltaTime;

            if (_overtakeTimer > overtakeInterval)
            {
                _isOvertaking = !_isOvertaking;
                _overtakeTimer = 0;
            }

            // if (_positionSwitchTimer > positionSwitchInterval) // Temporarily disabled for debugging
            // {
            //     _positionSwitchTimer = 0;
            //     _currentWingmanOffset.x *= -1;
            //     _currentWingmanOffset.y = Random.Range(2f, 10f);
            // }

            float angularVel = _targetRb ? _targetRb.angularVelocity.magnitude : 0;
            
            if (_isOvertaking && angularVel < 0.5f)
            {
                _currentWingmanOffset.z = Mathf.Lerp(_currentWingmanOffset.z, 20f, Time.deltaTime * 0.5f); 
            }
            else
            {
                _currentWingmanOffset.z = Mathf.Lerp(_currentWingmanOffset.z, -5f, Time.deltaTime * 0.5f); 
            }
        }
    }

    Vector3 GetLeadTargetPosition(float maxPredictionTime)
    {
        if (target == null) return transform.position + transform.forward * 100;

        if (!_targetRb) return target.position;

        float distance = Vector3.Distance(transform.position, target.position);
        float speed = flyer.CurrentSpeed;
        
        float timeToReach = Mathf.Clamp(distance / (speed + 10f), 0, maxPredictionTime);
        return target.position + _targetRb.linearVelocity * timeToReach;
    }

    Vector3 GetFireLeadPosition()
    {
        if (target == null) return transform.position + transform.forward * 100;
        
        if (!_targetRb) return target.position;

        float fireSpeed = combatActor.RangedProjectileSpeed;

        Vector3 headPos = flyer.HeadTransform.position;

        float distance = Vector3.Distance(headPos, target.position);
        
        // Fallback for linear velocity dot product 
        Vector3 myVel = Vector3.zero;
        if (_myRb != null) myVel = _myRb.linearVelocity;
        
        float relativeSpeed = fireSpeed + Vector3.Dot(myVel, (target.position - transform.position).normalized);
        float timeToHit = distance / Mathf.Max(relativeSpeed, 1f);
        
        return target.position + _targetRb.linearVelocity * timeToHit;
    }

    public DragonHitbox SelectBestStrikeHitbox(Transform enemyTarget)
    {
        if (enemyTarget == null) return null;

        DragonHitbox[] hitboxes = (enemyTarget == target)
            ? _targetHitboxes
            : enemyTarget.GetComponentsInChildren<DragonHitbox>();

        if (hitboxes == null || hitboxes.Length == 0) return null;

        DragonHitbox bestHitbox = null;
        float highestScore = -float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (var hitbox in hitboxes)
        {
            if (hitbox == null) continue;
            float distance = Vector3.Distance(myPos, hitbox.transform.position);
            float score = GetHitboxPriorityWeight(hitbox.bodyPartType) - (distance * 0.5f);

            if (score > highestScore)
            {
                highestScore = score;
                bestHitbox = hitbox;
            }
        }

        return bestHitbox;
    }

    /// <summary>
    /// Evaluates all DragonHitbox components on the target and returns the best position to strike.
    /// Factors in distance to the AI's core/head and the priority of the body part.
    /// Includes predictive velocity offset for moving targets.
    /// </summary>
    public Vector3 GetBestMeleeTarget(Transform enemyTarget)
    {
        if (enemyTarget == null) return transform.position + transform.forward * 10f;

        DragonHitbox[] hitboxes = (enemyTarget == target) ? _targetHitboxes : enemyTarget.GetComponentsInChildren<DragonHitbox>();
        
        if (hitboxes == null || hitboxes.Length == 0)
        {
            // Fallback to center mass if no hitboxes found
            Rigidbody rb = (enemyTarget == target) ? _targetRb : enemyTarget.GetComponent<Rigidbody>();
            if (rb) return enemyTarget.position + rb.linearVelocity * 0.2f;
            return enemyTarget.position;
        }

        DragonHitbox bestHitbox = null;
        float highestScore = -float.MaxValue;
        Vector3 myPos = transform.position; // Can refine to rigCon.allFootIKConstraints_old[0].data.root.position if needed

        foreach (var hitbox in hitboxes)
        {
            float distance = Vector3.Distance(myPos, hitbox.transform.position);
            
            // Score = Base Priority Weight - Distance penalty
            // Closer is better. Head is better than Tail.
            float score = GetHitboxPriorityWeight(hitbox.bodyPartType) - (distance * 0.5f);
            
            if (score > highestScore)
            {
                highestScore = score;
                bestHitbox = hitbox;
            }
        }

        if (bestHitbox != null)
        {
            // Add predictive aim based on enemy velocity and distance
            Vector3 targetVelocity = Vector3.zero;
            if (bestHitbox.mainHealth != null)
            {
                Rigidbody enemyRb = (enemyTarget == target) ? _targetRb : bestHitbox.mainHealth.GetComponent<Rigidbody>();
                if (enemyRb != null) targetVelocity = enemyRb.linearVelocity;
            }

            float distanceToHitbox = Vector3.Distance(myPos, bestHitbox.transform.position);
            float mySpeed = flyer != null ? flyer.CurrentSpeed : 20f;
            
            float closingSpeed = mySpeed;
            if (_myRb != null) 
            {
                Vector3 relativeVel = _myRb.linearVelocity - targetVelocity;
                closingSpeed = Vector3.Dot(relativeVel, (bestHitbox.transform.position - myPos).normalized);
            }
            
            // Limit prediction time to avoid erratic over-steering
            float leadTime = closingSpeed > 1f ? distanceToHitbox / closingSpeed : 0.2f;
            leadTime = Mathf.Clamp(leadTime, 0f, 1.5f);

            return bestHitbox.transform.position + (targetVelocity * leadTime);
        }

        return enemyTarget.position;
    }

        private float GetHitboxPriorityWeight(DragonHitbox.BodyPartType type)
    {
        switch (type)
        {
            case DragonHitbox.BodyPartType.Head: return 50f;
            case DragonHitbox.BodyPartType.Neck: return 40f;
            case DragonHitbox.BodyPartType.Body: return 20f;
            case DragonHitbox.BodyPartType.Wing: return -1000f; // Ігноруємо крила
            case DragonHitbox.BodyPartType.Tail: return -1000f; // Ігноруємо хвіст
            case DragonHitbox.BodyPartType.Leg:  return -1000f; // Ігноруємо лапи
            default: return 0f;
        }
    }

    Vector3 CalculateSteering()
    {
        Vector3 desiredDir = transform.forward;

        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);

            switch (currentState)
            {
                case AIState.Chase:
                    Vector3 chasePos = (distance < ramDistance) ? 
                        target.position + (target.position - transform.position).normalized * 5f : 
                        target.position;
                    desiredDir = (chasePos - transform.position).normalized;
                    break;

                case AIState.Intercept:
                    desiredDir = (_predictedTargetPos - transform.position).normalized;
                    break;

                case AIState.Wingman:
                    // 1. Calculate the desired position relative to the target
                    Vector3 wingmanPos = target.TransformPoint(_currentWingmanOffset);
                    Vector3 toWingmanPosDir = (wingmanPos - transform.position).normalized;

                    // 2. Get the target's current direction of travel
                    Vector3 targetForward = _targetRb != null && _targetRb.linearVelocity.sqrMagnitude > 1f 
                        ? _targetRb.linearVelocity.normalized 
                        : target.forward;

                    // 3. Blend the two directions. Prioritize matching direction when close, prioritize getting to position when far.
                    float distanceToPos = Vector3.Distance(transform.position, wingmanPos);
                    float blendFactor = Mathf.Clamp01(distanceToPos / 20f); // At 20m away, focus entirely on getting to the spot
                    desiredDir = Vector3.Slerp(targetForward, toWingmanPosDir, blendFactor);
                    break;

                case AIState.Flee:
                    // Old algorithm
                    // desiredDir = (transform.position - target.position).normalized;
                    // desiredDir += transform.right * Mathf.Sin(Time.time * 2f) * 0.5f;
                    // desiredDir.Normalize();
                    
                    // Fly away dynamically to gain an advantage for the next dive
                    if (distance < repositionDistance)
                    {
                        desiredDir = (transform.position - target.position).normalized;
                        
                        // Add some lateral evasion to avoid looking like a rigid vertical retreat
                        Vector3 rightEvade = transform.right * Mathf.Sin(Time.time * 2f);
                        desiredDir = (desiredDir + rightEvade * 0.5f).normalized;
                        
                        // Gentle upward bias, not a forced minimum
                        desiredDir.y += 0.3f; 
                        desiredDir.Normalize();
                    }
                    else
                    {
                        // Turn back towards target once far enough, allowing fuel to recharge on the approach
                        desiredDir = (target.position - transform.position).normalized;
                    }
                    break;
                     

                case AIState.TerminalStrike:
                    // Fly towards the predicted body position to intercept the target
                    // The body aims for the intercept point, while the limbs will stretch to the specific hitbox
                    desiredDir = (_predictedTargetPos - transform.position).normalized;
                    break;
            }
        }

        // Obstacle Avoidance
        Vector3 avoidance = Vector3.zero;
        
        Vector3 origin = flyer.HeadTransform.position;
        
        float maxAvoidanceMagnitude = 0f;

        Vector3[] rays = new Vector3[] {
            transform.forward,
            (transform.forward - transform.right * 0.5f).normalized,
            (transform.forward + transform.right * 0.5f).normalized,
            (transform.forward + transform.up * 0.7f).normalized, // Stronger Up bias
            (transform.forward - transform.up * 0.5f).normalized
        };

        foreach (var dir in rays)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, avoidDistance, obstacleMask))
            {
                float strength = 1f - (hit.distance / avoidDistance);
                Vector3 avoidForceVec = hit.normal * avoidForce * strength;
                avoidance += avoidForceVec;
                
                if (strength > maxAvoidanceMagnitude) maxAvoidanceMagnitude = strength;

                // Add a bit of Up force if we hit something in front to help clearing obstacles
                if (Vector3.Dot(dir, transform.forward) > 0.8f) avoidance += Vector3.up * avoidForce * 0.5f;
            }
        }

        // Altitude Control
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit, 200, groundMask))
        {
            if (groundHit.distance < targetAltitude)
            {
                float strength = 1f - (groundHit.distance / targetAltitude);
                avoidance += Vector3.up * strength * 4.0f;
                if (strength > maxAvoidanceMagnitude) maxAvoidanceMagnitude = strength;
            }
        }

        // *Todo: update DragonWaterInteraction (currently accepts only Dragon) to enable Water Avoidance.
        if (waterInteraction != null)
        {
            float distToWater = waterInteraction.DistanceToWaterSurface;
            // Only apply avoidance if we are ABOVE or NEAR the surface. 
            // If already deep underwater (<-5m), let the recovery logic take over.
            if (distToWater > -5f && distToWater < waterAvoidanceDistance)
            {
                // Normalize strength: 1 at surface/submerged, 0 at avoidance distance
                float strength = 1f - Mathf.Clamp01(distToWater / waterAvoidanceDistance);
                avoidance += Vector3.up * strength * waterAvoidanceForce;
                if (strength > maxAvoidanceMagnitude) maxAvoidanceMagnitude = strength;
            }
        }

        currentDangerLevel = maxAvoidanceMagnitude;

        return (desiredDir + avoidance).normalized;
    }

    void ConvertSteeringToInputs(Vector3 steering, ref FlyerInputData inputs)
    {
        // --- PITCH & YAW ---
        // Project steering vector onto the dragon's local xy plane to get pitch/yaw angles
        float pitchAngle = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(steering, transform.right), transform.right);
        float yawAngle = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(steering, transform.up), transform.up);

        // Map angles to input [-1, 1] (P-Gain)
        // Increased max angles from 45f to 90f for smoother, less aggressive proportional response
        float targetPitchInput = Mathf.Clamp(-pitchAngle / 90f, -1f, 1f);
        float targetYawInput = Mathf.Clamp(yawAngle / 90f, -1f, 1f);

        // --- ROLL (World-space Upright Stabilization + Banking) ---
        float sideSteerAmount = Vector3.Dot(transform.right, steering);
        
        // 1. Desired Up Vector: World up, but tilted into the turn for banking
        Vector3 desiredUp = (Vector3.up - transform.right * sideSteerAmount * 1.5f).normalized;

        // 2. Calculate Roll Error: Find the angle between our current up and the desired up, around our forward axis
        float rollError = Vector3.SignedAngle(transform.up, desiredUp, transform.forward);

        // 3. Convert Error to Input: Apply proportional control to correct the roll.
        // Decreased multiplier from 0.05 (20 deg max) to / 60f (60 deg max) for softer roll correction.
        float targetRollInput = Mathf.Clamp(-rollError / 60f, -1f, 1f);

        // --- SMOOTHING (Simulating Pilot Reaction Time / Internal Damping) ---
        // This prevents "Bang-Bang" oscillations by adding a low-pass filter to the AI's "joystick"
        float smoothingSpeed = 6f; 
        _smoothedPitchYaw = Vector2.Lerp(_smoothedPitchYaw, new Vector2(targetYawInput, targetPitchInput), Time.deltaTime * smoothingSpeed);
        _smoothedRoll = Mathf.Lerp(_smoothedRoll, targetRollInput, Time.deltaTime * smoothingSpeed);

        inputs.pitchYawInput = _smoothedPitchYaw;
        inputs.rollInput = _smoothedRoll;

        // --- MANEUVERABILITY BOOST (RMB pressed equivalent) ---
        // If the dragon needs to make a sharp turn (angle is large) or is actively firing at a target, 
        // activate RMB pressed mode to use xAccumulator for physical rotation independent of head aim.
        float totalDeviation = Mathf.Abs(pitchAngle) + Mathf.Abs(yawAngle);
        if (totalDeviation > 15f || (combatActor != null && combatActor.IsRangedAttacking) || currentState == AIState.TerminalStrike)
        {
            inputs.useSharpTurn = true;
            // When RMB is pressed, the flight state uses xAccumulator for yaw/pitch. 
        }
        else
        {
            // Small angle -> turn off RMB mode and let it fly smoothly towards mouseAim_wPos
            inputs.useSharpTurn = false;
        }

        // --- THROTTLE ---
        // For now, always fly forward. Utility AI will decide if we should be flapping/gliding/boosting.
        inputs.moveInput = new Vector2(0, 1);
        
        // За замовчуванням пілот завжди хоче махати крилами (щоб не впасти під час атаки)
        inputs.isFlapping = true; 
    }
    // ManageFire() removed as part of Utility AI refactor

        #if UNITY_EDITOR
private void OnDrawGizmosSelected()
{
    if (!Application.isPlaying) return;
        if (showGizmos && flyer != null)
        {
            if (_isRecovering) Gizmos.color = Color.magenta;
            else Gizmos.color = Color.yellow;
            
            // Gizmos.DrawWireSphere(flyer.currentAim, 1f); // Not easily accessible directly anymore, but aim point is visualized indirectly
            
            if (target)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_predictedTargetPos, 1.5f);
            }

            // НОВЕ: курс під час атаки
            if (combatActor != null && combatActor.IsAttacking)
            {
                // Лінія від птаха до predicted точки
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _predictedTargetPos);
                Gizmos.DrawWireSphere(transform.position + transform.forward * 5f, 0.5f); // куди дивиться ніс

                // Лінія фактичного вектору швидкості
                Rigidbody rb = _myRb != null ? _myRb : GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, transform.position + rb.linearVelocity.normalized * 10f);
                }
            }
        }
    }
#endif
}
/// <summary>
/// Advanced AI Pilot for Dragon flight control.
/// Implements various tactics including Chase, Intercept, Wingman, and Flee.
/// Now features Collision Recovery and Hover Combat (Copter mode).
/// </summary>
public enum AIState { 
    Chase,      // Direct pursuit (follows target's tail)
    Intercept,  // Collision course (predicts where target will be and cuts them off)
    Wingman,    // Friendly/Companion mode (flies parallel to target)
    Flee,       // Retreat mode (flies away when weakened)
    //Reposition, // Boom and Zoom: flies away to gain distance for another attack run
    TerminalStrike // Aggressive direct homing during the final moments of an attack
}