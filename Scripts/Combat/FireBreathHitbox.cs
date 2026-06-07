using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DragonWorld.Combat
{
    public class FireBreathHitbox : MonoBehaviour
    {
        [Tooltip("Reference to the DragonStateManager to apply damage and get settings")]
        public DragonStateManager stateManager;

        [Header("Spawn Settings")]
        [Tooltip("How often to spawn a virtual sphere (seconds)")]
        public float spawnRate = 0.05f;
        [Tooltip("Max virtual spheres at the same time")]
        public int maxProjectiles = 150;
        [Tooltip("Forward offset from the head bone to spawn projectiles (avoids self-collision)")]
        public float spawnOffset = 1.0f;
        
        [Tooltip("Number of stream points generated to be passed to Audio Controller for sound/impulse pooling")]
        public int streamPointsCount = 8;

        [Header("Physics Settings (Sync with VFX Graph)")]
        public float minInitialSpeed = 75f;
        public float maxInitialSpeed = 75f;
        [Tooltip("Inherit velocity factor from dragon movement (usually 1.0)")]
        public float inheritVelocityMultiplier = 1.0f;
        public float linearDrag = 1.76f; 
        [Tooltip("If 'Use Particle Size' is enabled for Drag in the VFX graph, specify the size of the visual particles here (e.g. 0.4) for precise math synchronization")]
        public float visualParticleSize = 0.4f;
        public float minLifetime = 1f;
        public float maxLifetime = 1f;

        [Header("Head Tracking Settings (Sync with VFX Graph)")]
        [Tooltip("Rotates particles based on the dragon's head movement during flight")]
        public bool enableHeadTracking = true;
        [Tooltip("Curve for the rotational force relative to the normalized lifetime of the particle (0..1)")]
        public AnimationCurve headTrackingCurve = new AnimationCurve(
            new Keyframe(0f, 0.8f), 
            new Keyframe(0.17f, 0.8f), 
            new Keyframe(0.35f, 0f), 
            new Keyframe(1f, 0f)
        );
        [Tooltip("Multiplier for the rotation speed for FPS-independent version (used only when the code is uncommented)")]
        public float headTrackingSpeed = 50f;
        
        [Header("Turbulence Settings")]
        public bool enableTurbulence = true;
        public float turbulenceIntensity = 25f;
        public float turbulenceFrequency = 5f;
        
        [Header("External Forces")]
        [Tooltip("How much external wind affects the fire projectiles")]
        public float externalWindInfluence = 1.0f;
        
        [Header("Size Settings")]
        public float startRadius = 0.48f;
        public float endRadius = 5.0f;

        [Header("Burning Settings")]
        [Tooltip("Minimum continuous exposure time (seconds) before applying damage and ignition")]
        public float minExposureToIgnite = 0.4f;
        [Tooltip("Initial minimum burning duration for a quick swipe")]
        public float initialBurnDuration = 1.5f;
        [Tooltip("Maximum burning duration cap")]
        public float maxBurnDuration = 6.0f;
        [Tooltip("How much burning time is added per damage tick when continuously hitting the target")]
        public float burnAddPerHit = 1.5f;

        private struct FireProjectile
        {
            public int id;
            public Vector3 position;
            public Vector3 velocity;
            public float age;
            public float lifetime;
            public bool isAlive;
            public bool isAudioTracked;
        }

        private FireProjectile[] _projectiles;
        private int _projectileCount = 0;
        private float _spawnTimer = 0f;
        private int _nextProjectileId = 0;

        private int _totalSpawnedProjectiles = 0; // Tracks total spawned over the lifetime of the breath

        // Internal logic
        private Dictionary<Health, float> _lastDamageTimes = new Dictionary<Health, float>();
        private Dictionary<Collider, float> _exposureTimes = new Dictionary<Collider, float>();
        
        // Data for VFX
        public Vector3 CurrentCollisionPoint { get; private set; }
        public Vector3 CurrentCollisionNormal { get; private set; }
        public bool IsHittingTarget { get; private set; }
        public bool IsHittingWater { get; private set; }
        public Vector3[] StreamLightPositions { get; private set; } = new Vector3[2];
        
        public struct AudioStreamData
        {
            public int id;
            public Vector3 position;
            public Vector3 velocity;
        }

        public List<AudioStreamData> AudioStreamDataList { get; private set; } = new List<AudioStreamData>();
        public List<Vector3> AudioImpactPositions { get; private set; } = new List<Vector3>();

        private Vector3 _closestPointThisFrame;
        private Vector3 _closestNormalThisFrame;
        private float _closestDistanceThisFrame;
        private bool _hitWaterThisFrame;
        private float _timeSinceLastHit = 100f;
        
        private List<Vector3> _impactPointsThisFrame = new List<Vector3>();
        
        private DragonWaterInteraction _cachedWaterInter;
        private Rigidbody _dragonRb;

        private void Awake()
        {
            // Disable existing collider if there is one (we don't need it anymore)
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            _projectiles = new FireProjectile[maxProjectiles];
            
            if (stateManager != null)
            {
                _dragonRb = stateManager.GetComponent<Rigidbody>();
                _cachedWaterInter = stateManager.GetComponentInChildren<DragonWaterInteraction>();
            }
                
            ResetCollisionData();
        }

        private void OnEnable()
        {
            _lastDamageTimes.Clear();
            _exposureTimes.Clear();
            _projectileCount = 0;
            _totalSpawnedProjectiles = 0;
            _spawnTimer = 0f;
            ResetCollisionData();
        }

        private void OnDisable()
        {
            _projectileCount = 0;
            _exposureTimes.Clear();
            ResetCollisionData();
        }

        private void Update()
        {
            if (stateManager == null) return;

            // Automatically disable the hitbox if fire breath stopped and all projectiles died
            if (!stateManager.isFireBreathing && _projectileCount == 0)
            {
                ResetCollisionData();
                UpdateVFXData();
                UpdateVFXController();
                gameObject.SetActive(false);
                return;
            }

            _timeSinceLastHit += Time.deltaTime;
            
            // Handle spawning
            if (stateManager.fire.headBone != null && stateManager.isFireBreathing)
            {
                _spawnTimer += Time.deltaTime;
                while (_spawnTimer >= spawnRate)
                {
                    _spawnTimer -= spawnRate;
                    SpawnProjectile();
                }
            }
            
            // Move and collide projectiles
            ProcessProjectiles(Time.deltaTime);
            
            UpdateVFXData();
            UpdateVFXController();
        }

        private void UpdateVFXController()
        {
            if (stateManager != null && stateManager.vfxController != null && stateManager.fire.headBone != null)
            {
                stateManager.vfxController.UpdateFireBreathParams(
                    -stateManager.fire.headBone.forward,
                    CurrentCollisionPoint,
                    CurrentCollisionNormal,
                    IsHittingTarget,
                    IsHittingWater,
                    StreamLightPositions
                );
            }
            
            if (stateManager != null && stateManager.audioController != null)
            {
                stateManager.audioController.UpdateFireBreathStream(AudioStreamDataList, AudioImpactPositions, IsHittingTarget);
            }
        }

        private void SpawnProjectile()
        {
            if (_projectileCount >= maxProjectiles) return;
            
            Vector3 headForward = -stateManager.fire.headBone.forward;
            Vector3 headPos = stateManager.fire.headBone.position + headForward * spawnOffset;
            
            float speed = Random.Range(minInitialSpeed, maxInitialSpeed);
            Vector3 initialVel = headForward * speed;
            
            // Inherit velocity
            if (_dragonRb != null)
            {
                float inherited = Vector3.Dot(_dragonRb.linearVelocity, headForward);
                if (inherited > 0)
                {
                    initialVel += headForward * (inherited * inheritVelocityMultiplier);
                }
            }
            
            // We want to distribute audio points evenly across the stream.
            // Using a Round Robin approach: every Nth projectile gets an audio point.
            // N is calculated based on how many projectiles we expect to live at once vs how many audio points we have.
            // Average lifespan is roughly (minLifetime+maxLifetime)/2.
            // Expected active projectiles = spawnRate * expectedLifetime
            float expectedActive = (spawnRate > 0) ? ((minLifetime + maxLifetime) / 2f) / spawnRate : maxProjectiles;
            int distributionFactor = Mathf.Max(1, Mathf.RoundToInt(expectedActive / streamPointsCount));
            
            bool trackAudio = (_totalSpawnedProjectiles % distributionFactor == 0);
            
            _projectiles[_projectileCount] = new FireProjectile
            {
                id = _nextProjectileId++,
                position = headPos,
                velocity = initialVel,
                age = 0f,
                lifetime = Random.Range(minLifetime, maxLifetime),
                isAlive = true,
                isAudioTracked = trackAudio
            };
            
            _projectileCount++;
            _totalSpawnedProjectiles++;
        }

        private void ProcessProjectiles(float dt)
        {
            // We want to find the closest hit point among all projectiles THIS frame to pass to VFX
            _closestDistanceThisFrame = float.MaxValue;
            bool hitSomethingThisFrame = false;
            _hitWaterThisFrame = false;

            _impactPointsThisFrame.Clear();

            HashSet<Collider> collidersHitThisFrame = new HashSet<Collider>();

            float waterHeight = float.MinValue;
            if (_cachedWaterInter == null && stateManager != null) 
                _cachedWaterInter = stateManager.GetComponentInChildren<DragonWaterInteraction>();
                
            if (_cachedWaterInter != null && _cachedWaterInter.waterSurface != null)
                waterHeight = _cachedWaterInter.waterSurface.transform.position.y;

            float timeOffset = Time.time * turbulenceFrequency;
             
            Vector3 headForward = stateManager.fire.headBone != null ? -stateManager.fire.headBone.forward : Vector3.forward;

            for (int i = 0; i < _projectileCount; i++)
            {
                if (!_projectiles[i].isAlive) continue;

                // 1. Update Physics
                _projectiles[i].age += dt;
                if (_projectiles[i].age >= _projectiles[i].lifetime)
                {
                    _projectiles[i].isAlive = false;
                    continue;
                }
                
                // Head Tracking (Inherit Velocity in Update)
                if (enableHeadTracking)
                {
                    float normalizedAge = Mathf.Clamp01(_projectiles[i].age / _projectiles[i].lifetime);
                    float curveValue = headTrackingCurve.Evaluate(normalizedAge);

                    if (curveValue > 0.001f)
                    {
                        float speed = _projectiles[i].velocity.magnitude;
                        if (speed > 0.001f)
                        {
                            Vector3 currentDir = _projectiles[i].velocity / speed;
                            
                            // --- ВЕРСІЯ 1-в-1 З VFX ГРАФОМ (залежна від FPS) ---
                            // Точно копіює твою поточну логіку: Lerp(CurrentDir, HeadForward, CurveValue)
                            Vector3 newDir = Vector3.Lerp(currentDir, headForward, curveValue).normalized;
                            
                            // --- ВЕРСІЯ НЕЗАЛЕЖНА ВІД FPS (розкоментуй, коли пофіксиш VFX Graph) ---
                            // Для цієї версії в VFX графі треба помножити CurveValue на Delta Time і на якийсь множник (напр. 50)
                            float t = 1f - Mathf.Exp(-curveValue * headTrackingSpeed * dt);
                            newDir = Vector3.Lerp(currentDir, headForward, t).normalized;

                            _projectiles[i].velocity = newDir * speed;
                        }
                    }
                }

                // Linear drag: v = v * (1 - drag * size * dt)
                float effectiveDrag = linearDrag * visualParticleSize;
                _projectiles[i].velocity *= Mathf.Clamp01(1f - effectiveDrag * dt);
                
                // Turbulence simulation
                if (enableTurbulence)
                {
                    Vector3 p = _projectiles[i].position;
                    // Simple 3D noise approximation based on PerlinNoise
                    float nx = Mathf.PerlinNoise(p.y * 0.1f + timeOffset, p.z * 0.1f + timeOffset) * 2f - 1f;
                    float ny = Mathf.PerlinNoise(p.x * 0.1f + timeOffset, p.z * 0.1f + timeOffset) * 2f - 1f;
                    float nz = Mathf.PerlinNoise(p.x * 0.1f + timeOffset, p.y * 0.1f + timeOffset) * 2f - 1f;
                    
                    Vector3 turbulenceForce = new Vector3(nx, ny, nz) * turbulenceIntensity;
                    _projectiles[i].velocity += turbulenceForce * dt;
                }

                // External wind simulation
                if (stateManager.ExternalWindVelocity.sqrMagnitude > 0.1f)
                {
                    _projectiles[i].velocity += stateManager.ExternalWindVelocity * externalWindInfluence * dt;
                }
                
                Vector3 prevPos = _projectiles[i].position;
                Vector3 newPos = prevPos + _projectiles[i].velocity * dt;
                _projectiles[i].position = newPos;

                float currentRadius = Mathf.Lerp(startRadius, endRadius, _projectiles[i].age / _projectiles[i].lifetime);
                
                Vector3 dir = newPos - prevPos;
                float dist = dir.magnitude;
                
                if (dist <= 0.001f) continue;
                Vector3 dirNorm = dir / dist;

                // 2. Check Water Collision
                if (waterHeight > float.MinValue)
                {
                    if (prevPos.y > waterHeight && newPos.y <= waterHeight)
                    {
                        // Calculate intersection point exactly on the water surface
                        float t = (prevPos.y - waterHeight) / (prevPos.y - newPos.y);
                        Vector3 hitPoint = Vector3.Lerp(prevPos, newPos, t);
                        
                        RecordVFXHit(hitPoint, Vector3.up, ref hitSomethingThisFrame, true);
                        _projectiles[i].isAlive = false; // Destroy projectile in water
                        continue;
                    }
                    else if (prevPos.y <= waterHeight)
                    {
                        // Already underwater, kill it
                        _projectiles[i].isAlive = false;
                        continue;
                    }
                }

                // 3. Check Physics Collision (SphereCast to avoid tunneling)
                // Ignore triggers.
                RaycastHit[] hits = Physics.SphereCastAll(prevPos, currentRadius, dirNorm, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                
                bool hitWall = false;
                foreach (var hit in hits)
                {
                    // explicitly ignore triggers to be absolutely sure
                    if (hit.collider.isTrigger)
                        continue;

                    // Ignore self
                    if (stateManager.myBodyParts.Contains(hit.collider.GetInstanceID()))
                        continue;

                    bool isEnemy = stateManager.enemyIs.Includes(hit.collider.gameObject.layer) || stateManager.fire.igniteIt.Includes(hit.collider.gameObject.layer);
                    
                    if (isEnemy)
                    {
                        // Handle spherecast starting inside collider (hit.point is zero)
                        Vector3 actualHitPoint = hit.point;
                        if (actualHitPoint == Vector3.zero)
                        {
                            actualHitPoint = hit.collider.ClosestPoint(prevPos);
                        }

                        // It's an enemy
                        collidersHitThisFrame.Add(hit.collider);
                        ApplyDamage(hit.collider, actualHitPoint, dt);
                        RecordVFXHit(actualHitPoint, hit.normal, ref hitSomethingThisFrame, false);
                        // Fire penetrates enemies, so we don't kill the projectile here
                    }
                    else if (hit.collider.gameObject.layer == stateManager.gameObject.layer)
                    {
                        // Ignore our own layer entirely (prevents hitting wings/body parts not in myBodyParts list)
                        continue;
                    }
                    else
                    {
                        Vector3 actualHitPoint = hit.point;
                        if (actualHitPoint == Vector3.zero)
                        {
                            actualHitPoint = hit.collider.ClosestPoint(prevPos);
                        }

                        // It's environment
                        hitWall = true;
                        RecordVFXHit(actualHitPoint, hit.normal, ref hitSomethingThisFrame, false);
                        break; // Stop processing further hits along the ray for this projectile
                    }
                }

                if (hitWall)
                {
                    _projectiles[i].isAlive = false; // Destroy projectile on wall hit
                    continue;
                }
            }

            // Clean up exposure times for colliders not hit this frame
            List<Collider> keysToRemove = new List<Collider>();
            foreach (var key in _exposureTimes.Keys)
            {
                if (!collidersHitThisFrame.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _exposureTimes.Remove(key);
            }

            // Calculate Stream Light positions based on active projectiles
            UpdateStreamLightPositions();
            
            // Populate audio tracking data
            AudioStreamDataList.Clear();
            for (int i = 0; i < _projectileCount; i++)
            {
                if (_projectiles[i].isAlive && _projectiles[i].isAudioTracked)
                {
                    AudioStreamDataList.Add(new AudioStreamData
                    {
                        id = _projectiles[i].id,
                        position = _projectiles[i].position,
                        velocity = _projectiles[i].velocity
                    });
                }
            }

            // Clean up dead projectiles by swapping with the last active one (O(1) removal)
            for (int i = _projectileCount - 1; i >= 0; i--)
            {
                if (!_projectiles[i].isAlive)
                {
                    _projectiles[i] = _projectiles[_projectileCount - 1];
                    _projectileCount--;
                }
            }

            if (hitSomethingThisFrame)
            {
                _timeSinceLastHit = 0f;
            }
        }

        private void UpdateStreamLightPositions()
        {
            if (_projectileCount == 0 || stateManager == null || stateManager.fire.headBone == null)
            {
                Vector3 headPos = stateManager != null && stateManager.fire.headBone != null ? stateManager.fire.headBone.position : Vector3.zero;
                StreamLightPositions[0] = headPos;
                StreamLightPositions[1] = headPos;
                return;
            }

            // For visuals, find intermediate points
            int index1 = _projectileCount / 3;
            int index2 = (_projectileCount * 2) / 3;
            
            StreamLightPositions[0] = _projectiles[index1].position;
            StreamLightPositions[1] = _projectiles[index2].position;
        }

        private void RecordVFXHit(Vector3 point, Vector3 normal, ref bool hitFlag, bool isWater)
        {
            float distFromMouth = Vector3.Distance(stateManager.fire.headBone.position, point);
            if (distFromMouth < _closestDistanceThisFrame)
            {
                _closestDistanceThisFrame = distFromMouth;
                _closestPointThisFrame = point;
                _closestNormalThisFrame = normal;
                _hitWaterThisFrame = isWater;
                hitFlag = true;
            }
            
            // Add to impact clusters for audio
            bool isClustered = false;
            for (int i = 0; i < _impactPointsThisFrame.Count; i++)
            {
                if (Vector3.Distance(_impactPointsThisFrame[i], point) < 5f)
                {
                    isClustered = true;
                    // Move cluster slightly towards new point
                    _impactPointsThisFrame[i] = Vector3.Lerp(_impactPointsThisFrame[i], point, 0.5f);
                    break;
                }
            }
            if (!isClustered && _impactPointsThisFrame.Count < 3) // Max 3 impact audio points
            {
                _impactPointsThisFrame.Add(point);
            }
        }

        private void ApplyDamage(Collider enemyCollider, Vector3 hitPoint, float dt)
        {
            if (!stateManager.fire.ignite) return;

            // Track exposure
            if (!_exposureTimes.ContainsKey(enemyCollider))
            {
                _exposureTimes[enemyCollider] = 0f;
            }
            _exposureTimes[enemyCollider] += dt;

            // Check if minimum exposure is reached
            if (_exposureTimes[enemyCollider] < minExposureToIgnite)
            {
                return;
            }

            var targetHealth = enemyCollider.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                float fireRate = stateManager.fire.weaponData != null ? stateManager.fire.weaponData.fireRate : 0.5f;
                float timeSinceLastTick = Time.time - (_lastDamageTimes.ContainsKey(targetHealth) ? _lastDamageTimes[targetHealth] : 0f);
                
                if (timeSinceLastTick >= fireRate)
                {
                    _lastDamageTimes[targetHealth] = Time.time;
                    float damage = stateManager.fire.weaponData != null ? stateManager.fire.weaponData.damage : 0.5f;
                    
                    // Apply damage INSTANTLY
                    targetHealth.TakeDamage(damage);

                    // Apply burning accumulation
                    var targetThreatReceiver = targetHealth.GetComponentInParent<DragonWorld.AI.Utility.IThreatReceiver>();
                    if (targetThreatReceiver != null)
                    {
                        // Add time dynamically, capped at maxBurnDuration
                        targetThreatReceiver.StartBurning(burnAddPerHit, enemyCollider, hitPoint, maxBurnDuration, initialBurnDuration, stateManager != null ? stateManager.gameObject : null);
                    }
                }
            }
        }

        private void UpdateVFXData()
        {
            if (_timeSinceLastHit < 0.1f)
            {
                IsHittingTarget = true;
                IsHittingWater = _hitWaterThisFrame;
                CurrentCollisionPoint = _closestPointThisFrame;
                CurrentCollisionNormal = _closestNormalThisFrame;
                
                AudioImpactPositions.Clear();
                AudioImpactPositions.AddRange(_impactPointsThisFrame);
            }
            else
            {
                IsHittingTarget = false;
                IsHittingWater = false;
                // Move the collision plane far below the world when not hitting anything 
                // to prevent VFX Graph particles from colliding with it and spawning incorrectly.
                CurrentCollisionPoint = new Vector3(0, -10000f, 0);
                CurrentCollisionNormal = Vector3.up;
                
                AudioImpactPositions.Clear();
            }
        }

        public void ResetCollisionData()
        {
            CurrentCollisionPoint = new Vector3(0, -10000f, 0); // Far away below the world
            CurrentCollisionNormal = Vector3.up;
            IsHittingTarget = false;
            IsHittingWater = false;
            _hitWaterThisFrame = false;
            _timeSinceLastHit = 100f;
            StreamLightPositions[0] = Vector3.zero;
            StreamLightPositions[1] = Vector3.zero;
            
            AudioStreamDataList.Clear();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _projectiles == null) return;
            if (!stateManager.showHUD) return;
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Semi-transparent orange
            for (int i = 0; i < _projectileCount; i++)
            {
                if (_projectiles[i].isAlive)
                {
                    float currentRadius = Mathf.Lerp(startRadius, endRadius, _projectiles[i].age / _projectiles[i].lifetime);
                    Gizmos.DrawWireSphere(_projectiles[i].position, currentRadius);
                }
            }

            // Visualize Current Collision Point for debugging
            if (IsHittingTarget)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(CurrentCollisionPoint, 1.0f);
                Gizmos.DrawLine(CurrentCollisionPoint, CurrentCollisionPoint + CurrentCollisionNormal * 2f);
            }
        }
    }
}
