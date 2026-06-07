using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Implemented by actors (Dragon, Bird, Rider) that can receive combat threats and cinematic attacks.
    /// Used to decouple the environment/enemy attacks (like Lightning or Talon Strikes) from specific classes.
    /// </summary>
    public interface IThreatReceiver
    {
        GameObject GameObject { get; }
        
        bool IsPerfectDodgeWindow { get; }
        bool IsAIControlled { get; }
        bool IsFireBreathing { get; }

        void SetUnderThreat(float duration);
        void TriggerPerfectDodge();
        void ApplyShockState(float intensity);
        void ModifyStamina(float amount);
        
        void StartBurning(float addedDuration, Collider hitCollider, Vector3 hitPosition, float maxDuration = 8f, float initialDuration = 1.5f, GameObject attacker = null);
        bool IsBurning();
        
        // For cinematic focus
        void AddTemporaryTarget(Transform target, float duration, float weight, float radius);
        void RemoveTemporaryTarget(Transform target);
    }
}