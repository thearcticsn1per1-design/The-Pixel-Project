using UnityEngine;

namespace PixelProject.Combat
{
    /// <summary>
    /// Interface for any entity that can receive damage.
    /// </summary>
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }

        void TakeDamage(DamageInfo damageInfo);
        void Heal(float amount);
    }

    /// <summary>
    /// Contains all information about a damage instance.
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        public float Damage;
        public bool IsCritical;
        public Vector3 Source;
        public Vector2 Direction;
        public DamageType DamageType;
        public string DamageSource;
        public GameObject Instigator;

        public static DamageInfo Create(float damage, Vector3 source)
        {
            return new DamageInfo
            {
                Damage = damage,
                Source = source,
                Direction = Vector2.zero,
                DamageType = DamageType.Physical
            };
        }
    }

    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Explosive,
        True // Ignores armor
    }

    /// <summary>
    /// Utility class for damage calculations.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// Calculate final damage after armor reduction.
        /// </summary>
        public static float CalculatePhysicalDamage(float baseDamage, float armor)
        {
            // Armor formula: reduction = armor / (armor + 100)
            float reduction = armor / (armor + 100f);
            return baseDamage * (1f - reduction);
        }

        /// <summary>
        /// Calculate elemental damage with resistance.
        /// </summary>
        public static float CalculateElementalDamage(float baseDamage, float resistance)
        {
            // Resistance is a percentage (0-1)
            return baseDamage * (1f - Mathf.Clamp01(resistance));
        }

        /// <summary>
        /// Calculate damage with critical hit.
        /// </summary>
        public static float ApplyCritical(float baseDamage, float critMultiplier)
        {
            return baseDamage * critMultiplier;
        }

        /// <summary>
        /// Calculate damage falloff over distance.
        /// </summary>
        public static float ApplyFalloff(float baseDamage, float distance, float maxDistance, float minDamagePercent = 0.3f)
        {
            float falloff = 1f - Mathf.Clamp01(distance / maxDistance);
            return baseDamage * Mathf.Max(falloff, minDamagePercent);
        }
    }
}
