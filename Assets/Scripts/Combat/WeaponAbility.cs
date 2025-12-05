using UnityEngine;

namespace PixelProject.Combat
{
    /// <summary>
    /// Base class for weapon secondary abilities.
    /// </summary>
    public abstract class WeaponAbility : ScriptableObject
    {
        public string abilityName;
        [TextArea] public string description;
        public float cooldown = 5f;
        public Sprite icon;

        public abstract void Execute(GameObject owner);
    }

    /// <summary>
    /// Grenade launcher ability that fires an explosive projectile.
    /// </summary>
    [CreateAssetMenu(fileName = "Grenade Ability", menuName = "Pixel Project/Abilities/Grenade")]
    public class GrenadeAbility : WeaponAbility
    {
        public GameObject grenadePrefab;
        public float throwForce = 10f;
        public float damage = 50f;
        public float explosionRadius = 3f;

        public override void Execute(GameObject owner)
        {
            if (grenadePrefab == null) return;

            var firePoint = owner.GetComponentInChildren<Transform>();
            var controller = owner.GetComponentInParent<Player.PlayerController>();

            Vector2 direction = controller?.AimDirection ?? Vector2.right;

            GameObject grenade = Instantiate(grenadePrefab, firePoint.position, Quaternion.identity);
            var rb = grenade.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.AddForce(direction * throwForce, ForceMode2D.Impulse);
            }

            var explosive = grenade.GetComponent<ExplosiveProjectile>();
            if (explosive != null)
            {
                explosive.Initialize(damage, explosionRadius);
            }
        }
    }

    /// <summary>
    /// Dash attack ability that damages enemies in path.
    /// </summary>
    [CreateAssetMenu(fileName = "Dash Attack", menuName = "Pixel Project/Abilities/Dash Attack")]
    public class DashAttackAbility : WeaponAbility
    {
        public float dashDistance = 5f;
        public float damage = 30f;
        public float dashSpeed = 20f;

        public override void Execute(GameObject owner)
        {
            var controller = owner.GetComponentInParent<Player.PlayerController>();
            if (controller == null) return;

            // The dash attack would be implemented with coroutine or state machine
            Debug.Log($"Executing dash attack for {damage} damage!");
        }
    }

    /// <summary>
    /// Shield ability that blocks damage temporarily.
    /// </summary>
    [CreateAssetMenu(fileName = "Shield Ability", menuName = "Pixel Project/Abilities/Shield")]
    public class ShieldAbility : WeaponAbility
    {
        public float duration = 2f;
        public float damageReduction = 0.5f;
        public GameObject shieldEffectPrefab;

        public override void Execute(GameObject owner)
        {
            var stats = owner.GetComponentInParent<Player.PlayerStats>();
            if (stats == null) return;

            // Add temporary damage reduction
            var modifier = new Player.StatModifier(
                -damageReduction,
                Player.ModifierType.Multiplicative,
                "shield_ability",
                duration
            );
            stats.AddModifier(Player.StatType.Armor, modifier);

            if (shieldEffectPrefab != null)
            {
                var effect = Instantiate(shieldEffectPrefab, owner.transform);
                Destroy(effect, duration);
            }

            Debug.Log($"Shield activated for {duration} seconds!");
        }
    }
}
