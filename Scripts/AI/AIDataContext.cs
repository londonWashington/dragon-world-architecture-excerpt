using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Context data for the Utility AI brain.
    /// Gathers snapshot of the world and dragon state every frame.
    /// </summary>
    public class AIDataContext
    {
        // Dragon State
        public float staminaPercentage;
        public float healthPercentage;
        public float currentSpeed;
        public float rollAngle;

        // World Data
        public float distanceToTarget;
        public float distanceToWater;
        public float targetSpeed;
        public float dangerLevel;
        public bool isTargetInLineOfSight;
        public bool isUnderWater;

        // Combat Data
        public bool isAttacking;
        public float angleToTarget;
        public Transform target; // Added target reference
        
        // Fire Breath / Resources
        public bool hasFuelForFireBreath;
        public float fuelPercentage; // 0 to 1
        public bool isFireBreathing;

        // Tactical Data
        public float targetBlindSpotAngle; // Angle between target's forward and vector to dragon. 180 = directly behind target (safe).
        public float targetHealthPercentage;
        public float myHealthPercentage;
        public bool isTargetInShock; // True if target is knocked down/stunned

        // References (Optional, but useful for actions to access systems if needed)
        public IUtilityFlyer flyer;
        public ICombatActor combatActor;
        public DragonAIPilot aiPilot;
        public DragonWaterInteraction waterInteraction;

        // Current Input Package to be sent to Flyer
        public FlyerInputData currentInputData;
    }
}
