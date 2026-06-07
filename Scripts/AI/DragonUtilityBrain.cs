using System.Collections.Generic;
using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Component placed on the Dragon to manage decision-making using Utility AI.
    /// Replaces hard-coded logic for abilities, boost, and stamina regeneration.
    /// </summary>
    public class DragonUtilityBrain : MonoBehaviour
    {
        [Tooltip("List of possible actions this dragon can perform.")]
        public List<DragonAIAction> availableActions;

        private AIDataContext _context;               // Приватна змінна для самого мозку
        public AIDataContext Context => _context;     // Публічне посилання, щоб Пілот міг її читати
        private DragonAIAction _currentBestAction;

        private void Start()
        {
            _context = new AIDataContext();
            _context.flyer = GetComponent<IUtilityFlyer>();
            _context.combatActor = GetComponent<ICombatActor>();
            _context.aiPilot = GetComponent<DragonAIPilot>();
            _context.waterInteraction = GetComponent<DragonWaterInteraction>();

            if (_context.flyer == null) Debug.LogError("IUtilityFlyer not found on this GameObject! Ensure it implements the interface.");
            if (_context.combatActor == null) Debug.LogWarning("ICombatActor not found. This flyer cannot perform attacks.");
            if (_context.aiPilot == null) Debug.LogError("DragonAIPilot not found on this GameObject!");
        }

        private void Update()
        {
            // 1. Gather Context (update data snapshot)
            UpdateContext();

            // 2. Decide Best Action
            DecideBestAction();

            // 3. Execute Best Action
            ExecuteAction();
        }
        /// <summary>
        /// Populates the AI Data Context with current world and state data.
        /// </summary>
        private void UpdateContext()
        {
            if (_context.flyer == null || _context.aiPilot == null) return;

            // Скидаємо ТІЛЬКИ команди дій (щоб вони не "залипали"). 
            // Не можна робити new FlyerInputData(), бо це зітре aimPosition Пілота!
            _context.currentInputData.isFlapping = false;
            _context.currentInputData.wantsToGainAltitude = false;

            // Flyer State
            _context.staminaPercentage = _context.flyer.StaminaPercentage;
            _context.myHealthPercentage = _context.flyer.HealthPercentage;
            _context.currentSpeed = _context.flyer.CurrentSpeed;
            _context.dangerLevel = _context.aiPilot.currentDangerLevel;
            _context.isUnderWater = _context.flyer.IsUnderwater;
            _context.rollAngle = _context.flyer.RollAngle;

            // World Data
            if (_context.aiPilot.target != null)
            {
                _context.target = _context.aiPilot.target;
                _context.distanceToTarget = Vector3.Distance(transform.position, _context.aiPilot.target.position);
                Rigidbody targetRb = _context.aiPilot.target.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    _context.targetSpeed = targetRb.linearVelocity.magnitude;
                }
                
                // Target Health and Shock status
                DragonWorld.Combat.Health targetHealth = _context.aiPilot.target.GetComponentInParent<DragonWorld.Combat.Health>();
                if (targetHealth != null)
                {
                    _context.targetHealthPercentage = targetHealth.currentHealth / Mathf.Max(1f, targetHealth.maxHealth);
                }
                else
                {
                    _context.targetHealthPercentage = 1f; // Default if no health component
                }

                DragonStateManager targetDragon = _context.aiPilot.target.GetComponentInParent<DragonStateManager>();
                if (targetDragon != null)
                {
                    _context.isTargetInShock = targetDragon.isShocked;
                }
                else
                {
                    _context.isTargetInShock = false; // Fallback
                }
                
                // Calculate angle to target
                Vector3 directionToTarget = _context.aiPilot.target.position - transform.position;
                _context.angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

                // Calculate blind spot angle
                _context.targetBlindSpotAngle = Vector3.Angle(_context.aiPilot.target.forward, -directionToTarget);

                // Line of Sight
                if (_context.flyer.RootTransform != null)
                {
                    // Fallback to testing line of sight from the root of the flyer + an offset
                    Vector3 viewPos = _context.flyer.RootTransform.position + (Vector3.up * 2f);
                    Vector3 targetPos = _context.aiPilot.target.position;
                    _context.isTargetInLineOfSight = !Physics.Linecast(viewPos, targetPos, _context.aiPilot.obstacleMask | _context.aiPilot.groundMask, QueryTriggerInteraction.Ignore);
                }
                else
                {
                    _context.isTargetInLineOfSight = true; // Fallback
                }
            }
            else
            {
                _context.target = null;
                _context.distanceToTarget = float.MaxValue;
                _context.targetSpeed = 0f;
                _context.angleToTarget = 180f; // Default to max angle when no target
                _context.targetBlindSpotAngle = 0f;
                _context.isTargetInLineOfSight = false;
                _context.targetHealthPercentage = 1f;
                _context.isTargetInShock = false;
            }

            // Combat State
            if (_context.combatActor != null)
            {
                _context.isAttacking = _context.combatActor.IsAttacking;
                _context.isFireBreathing = _context.combatActor.IsRangedAttacking;
                _context.hasFuelForFireBreath = _context.combatActor.HasRangedAmmo;
                _context.fuelPercentage = _context.combatActor.RangedAmmoPercentage;
            }
            else
            {
                _context.isAttacking = false;
                _context.isFireBreathing = false;
                _context.hasFuelForFireBreath = false;
                _context.fuelPercentage = 0f;
            }

            if (_context.waterInteraction != null)
            {
                _context.distanceToWater = _context.waterInteraction.DistanceToWaterSurface;
            }
            else
            {
                _context.distanceToWater = float.MaxValue;
            }
        }

        /// <summary>
        /// Evaluates all available actions and selects the one with the highest score.
        /// </summary>
        private void DecideBestAction()
        {
            if (availableActions == null || availableActions.Count == 0) return;

            float highestScore = -1f;
            DragonAIAction bestAction = null;

            foreach (var action in availableActions)
            {
                if (action == null) continue;

                float score = action.Evaluate(_context);

                if (score > highestScore)
                {
                    highestScore = score;
                    bestAction = action;
                }
            }

            if (_currentBestAction != bestAction)
            {
                if (_currentBestAction != null)
                {
                    _currentBestAction.OnActionExited(_context);
                }
                _currentBestAction = bestAction;
            }
        }

        /// <summary>
        /// Executes the currently selected best action.
        /// </summary>
        private void ExecuteAction()
        {
            if (_currentBestAction != null)
            {
                _currentBestAction.Execute(_context);
            }
        }
    }
}
