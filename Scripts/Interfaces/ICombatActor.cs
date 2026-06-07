using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Interface representing an actor capable of performing combat actions (Melee/Ranged).
    /// Decouples AI decision making from the specific combat implementation of the creature.
    /// </summary>
    public interface ICombatActor
    {
        // Status
        bool IsAttacking { get; }
        
        // Ranged/Breath
        bool IsRangedAttacking { get; }
        bool HasRangedAmmo { get; }
        float RangedAmmoPercentage { get; } // 0 to 1
        float RangedProjectileSpeed { get; }
        
        // Methods
        void ExecuteMeleeAttack(Vector3 targetPosition);
        
        /// <summary>
        /// Continuously fires ranged weapon / breath towards target.
        /// </summary>
        void ExecuteRangedAttack(Vector3 targetPosition, bool isFiring);
    }
}
