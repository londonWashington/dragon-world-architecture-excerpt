using System.Collections.Generic;
using UnityEngine;
using DragonWorld.Targeting;

namespace DragonWorld.Combat
{
    /// <summary>
    /// Marker component for objects that can be targeted as Enemies in combat.
    /// Auto-registers itself to the global TargetRegistry.
    /// </summary>
    public class Targetable : MonoBehaviour
    {
        // Legacy list kept for backward compatibility if other scripts still reference it directly, 
        // though they should migrate to TargetRegistry.Enemies.
        public static List<Targetable> AllTargetables = new List<Targetable>();

        public Health health;

        void Awake()
        {
            if (!health) health = GetComponentInParent<Health>();
        }

        private void OnEnable()
        {
            // Register to global AAA registry
            TargetRegistry.RegisterEnemy(this);

            // Legacy compatibility
            if (!AllTargetables.Contains(this))
            {
                AllTargetables.Add(this);
            }
        }

        private void OnDisable()
        {
            // Unregister from global AAA registry
            TargetRegistry.UnregisterEnemy(this);

            // Legacy compatibility
            if (AllTargetables.Contains(this))
            {
                AllTargetables.Remove(this);
            }
        }
    }
}
