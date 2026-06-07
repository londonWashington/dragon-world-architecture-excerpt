using UnityEngine;

namespace DragonWorld.Combat
{
    public struct DamageInfo
    {
        public float Damage;
        public Vector3 HitDirection;
        public float ImpactForce;
        public Vector3 HitPoint;
        public GameObject Instigator;

        public DamageInfo(float damage, Vector3 hitDirection, float impactForce, Vector3 hitPoint, GameObject instigator = null)
        {
            Damage = damage;
            HitDirection = hitDirection;
            ImpactForce = impactForce;
            HitPoint = hitPoint;
            Instigator = instigator;
        }
    }
}