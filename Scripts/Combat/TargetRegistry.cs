using System.Collections.Generic;
using UnityEngine;

namespace DragonWorld.Targeting
{
    /// <summary>
    /// AAA-level central registry for all targetable entities in the world.
    /// Replaces scattered static lists and manual GameObject.Find/Tags.
    /// </summary>
    public static class TargetRegistry
    {
        // Lists of components, not raw transforms, to avoid GetComponent calls
        public static HashSet<DragonWorld.Combat.Targetable> Enemies = new HashSet<DragonWorld.Combat.Targetable>();
        public static HashSet<PointOfInterest> POIs = new HashSet<PointOfInterest>();

        public static void RegisterEnemy(DragonWorld.Combat.Targetable enemy)
        {
            if (enemy != null) Enemies.Add(enemy);
        }

        public static void UnregisterEnemy(DragonWorld.Combat.Targetable enemy)
        {
            if (enemy != null) Enemies.Remove(enemy);
        }

        public static void RegisterPOI(PointOfInterest poi)
        {
            if (poi != null) POIs.Add(poi);
        }

        public static void UnregisterPOI(PointOfInterest poi)
        {
            if (poi != null) POIs.Remove(poi);
        }
    }
}