using UnityEngine;
using System.Collections.Generic;
using PixelProject.Core;
using PixelProject.Player;

namespace PixelProject.Items
{
    /// <summary>
    /// ScriptableObject defining item properties and effects.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Pixel Project/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string itemId;
        public string itemName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Properties")]
        public ItemType itemType = ItemType.Passive;
        public ItemRarity rarity = ItemRarity.Common;
        public bool isStackable = true;
        public int maxStack = 99;

        [Header("Stat Modifiers")]
        public List<ItemStatModifier> statModifiers = new List<ItemStatModifier>();

        [Header("Special Effects")]
        public ItemEffect specialEffect;

        [Header("Acquisition")]
        public int dropWeight = 10;
        public int shopPrice = 100;
        public int unlockCost = 0; // Meta currency to unlock
        public bool startsUnlocked = true;

        [Header("Visuals")]
        public GameObject worldPrefab;
        public Color rarityColor = Color.white;

        public string GetFormattedDescription()
        {
            string desc = description;

            foreach (var mod in statModifiers)
            {
                string sign = mod.value >= 0 ? "+" : "";
                string valueStr = mod.modifierType == ModifierType.Multiplicative
                    ? $"{sign}{mod.value * 100:F0}%"
                    : $"{sign}{mod.value:F0}";

                desc += $"\n{mod.statType}: {valueStr}";
            }

            return desc;
        }
    }

    [System.Serializable]
    public class ItemStatModifier
    {
        public StatType statType;
        public float value;
        public ModifierType modifierType = ModifierType.Flat;
    }

    public enum ItemType
    {
        Passive,        // Always active when held
        Active,         // Has an activatable ability
        Consumable,     // Single use
        Weapon,         // Weapon item
        Currency        // Gold, experience orbs, etc.
    }

    /// <summary>
    /// Base class for special item effects.
    /// </summary>
    public abstract class ItemEffect : ScriptableObject
    {
        public string effectName;
        [TextArea] public string effectDescription;

        public abstract void OnPickup(PlayerStats player);
        public abstract void OnRemove(PlayerStats player);
        public virtual void OnUpdate(PlayerStats player) { }
        public virtual void OnPlayerDamaged(PlayerStats player, float damage) { }
        public virtual void OnPlayerAttack(PlayerStats player, float damage) { }
        public virtual void OnEnemyKilled(PlayerStats player) { }
    }

    /// <summary>
    /// Effect that triggers when player health drops below threshold.
    /// </summary>
    [CreateAssetMenu(fileName = "Low Health Effect", menuName = "Pixel Project/Effects/Low Health")]
    public class LowHealthEffect : ItemEffect
    {
        public float healthThreshold = 0.3f;
        public StatType buffStat = StatType.Damage;
        public float buffValue = 0.5f;
        public ModifierType buffType = ModifierType.Multiplicative;

        private bool isActive = false;
        private string modifierSource;

        public override void OnPickup(PlayerStats player)
        {
            modifierSource = $"lowhealth_{effectName}_{GetInstanceID()}";
        }

        public override void OnRemove(PlayerStats player)
        {
            if (isActive)
            {
                player.RemoveModifier(buffStat, modifierSource);
                isActive = false;
            }
        }

        public override void OnUpdate(PlayerStats player)
        {
            bool shouldBeActive = player.HealthPercent <= healthThreshold;

            if (shouldBeActive && !isActive)
            {
                player.AddModifier(buffStat, new StatModifier(buffValue, buffType, modifierSource));
                isActive = true;
            }
            else if (!shouldBeActive && isActive)
            {
                player.RemoveModifier(buffStat, modifierSource);
                isActive = false;
            }
        }
    }

    /// <summary>
    /// Effect that grants bonus on kill.
    /// </summary>
    [CreateAssetMenu(fileName = "On Kill Effect", menuName = "Pixel Project/Effects/On Kill")]
    public class OnKillEffect : ItemEffect
    {
        public OnKillBonusType bonusType = OnKillBonusType.Heal;
        public float bonusValue = 5f;
        public float duration = 3f; // For temporary buffs

        public override void OnPickup(PlayerStats player) { }
        public override void OnRemove(PlayerStats player) { }

        public override void OnEnemyKilled(PlayerStats player)
        {
            switch (bonusType)
            {
                case OnKillBonusType.Heal:
                    player.Heal(bonusValue);
                    break;
                case OnKillBonusType.Gold:
                    player.AddGold(Mathf.RoundToInt(bonusValue));
                    break;
                case OnKillBonusType.Experience:
                    player.AddExperience(Mathf.RoundToInt(bonusValue));
                    break;
                case OnKillBonusType.TemporaryDamage:
                    player.AddModifier(StatType.Damage, new StatModifier(
                        bonusValue / 100f, ModifierType.Multiplicative, "onkill_damage", duration
                    ));
                    break;
                case OnKillBonusType.TemporarySpeed:
                    player.AddModifier(StatType.MoveSpeed, new StatModifier(
                        bonusValue / 100f, ModifierType.Multiplicative, "onkill_speed", duration
                    ));
                    break;
            }
        }
    }

    public enum OnKillBonusType
    {
        Heal,
        Gold,
        Experience,
        TemporaryDamage,
        TemporarySpeed
    }

    /// <summary>
    /// Effect that modifies projectiles.
    /// </summary>
    [CreateAssetMenu(fileName = "Projectile Effect", menuName = "Pixel Project/Effects/Projectile")]
    public class ProjectileEffect : ItemEffect
    {
        public ProjectileModType modType = ProjectileModType.Pierce;
        public int additionalProjectiles = 0;
        public float sizeMultiplier = 1f;
        public bool homing = false;
        public bool explosive = false;
        public float explosionRadius = 2f;

        public override void OnPickup(PlayerStats player)
        {
            if (additionalProjectiles > 0)
            {
                player.AddModifier(StatType.ProjectileCount, new StatModifier(
                    additionalProjectiles, ModifierType.Flat, $"proj_{effectName}"
                ));
            }

            if (sizeMultiplier != 1f)
            {
                player.AddModifier(StatType.AreaOfEffect, new StatModifier(
                    sizeMultiplier - 1f, ModifierType.Multiplicative, $"proj_{effectName}"
                ));
            }
        }

        public override void OnRemove(PlayerStats player)
        {
            player.RemoveAllModifiersFromSource($"proj_{effectName}");
        }
    }

    public enum ProjectileModType
    {
        Pierce,
        Split,
        Bounce,
        Chain
    }
}
