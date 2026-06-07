using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Interface representing a flying entity that the Utility AI can control.
    /// It provides status information and receives FlyerInputData to execute movement.
    /// </summary>
    public interface IUtilityFlyer
    {
        // Core Stats
        float Stamina { get; }
        float StaminaPercentage { get; }
        float HealthPercentage { get; }
        
        // Movement Data
        float CurrentSpeed { get; }
        float RollAngle { get; }
        bool IsUnderwater { get; }
        
        // Context 
        Transform RootTransform { get; }
        Transform HeadTransform { get; }
        
        // Status
        bool IsStunned { get; }

        /// <summary>
        /// Sends a package of movement commands to the flyer's controller (e.g. AircraftPhysics, Animator).
        /// </summary>
        void ProcessAIInput(FlyerInputData input);
    }
}
