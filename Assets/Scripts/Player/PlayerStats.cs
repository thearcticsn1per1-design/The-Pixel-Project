using UnityEngine;
using System;
using System.Collections.Generic;
using PixelProject.Core;

namespace PixelProject.Player
{
    /// <summary>
    /// Manages all player statistics including health, damage, and various multipliers.
    /// Supports temporary and permanent stat modifications.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private float baseMaxHealth = 100f;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float baseAttackSpeed = 1f;
        [SerializeField] private float baseCritChance = 0.05f;
        [SerializeField] private float baseCritDamage = 1.5f;
        [SerializeField] private float baseArmor = 0f;
        [SerializeField] private float baseHealthRegen = 0f;

        [Header("Experience")]
        [SerializeField] private int baseExpToLevel = 100;
        [SerializeField] private float expScaling = 1.2f;

        // Current values
        public float CurrentHealth { get; private set; }
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int Gold { get; private set; }

        // Calculated stats with multipliers
        public float MaxHealth => CalculateStat(baseMaxHealth, StatType.MaxHealth);
        public float Damage => CalculateStat(baseDamage, StatType.Damage);
        public float AttackSpeed => CalculateStat(baseAttackSpeed, StatType.AttackSpeed);
        public float CritChance => Mathf.Min(1f, CalculateStat(baseCritChance, StatType.CritChance));
        public float CritDamage => CalculateStat(baseCritDamage, StatType.CritDamage);
        public float Armor => CalculateStat(baseArmor, StatType.Armor);
        public float HealthRegen => CalculateStat(baseHealthRegen, StatType.HealthRegen);
        public float MoveSpeedMultiplier => GetMultiplier(StatType.MoveSpeed);
        public float DashSpeedMultiplier => GetMultiplier(StatType.DashSpeed);
        public float DashCooldownMultiplier => GetMultiplier(StatType.DashCooldown);
        public float PickupRange => CalculateStat(2f, StatType.PickupRange);

        public bool CanDash => true; // Could be gated by unlock

        public int ExpToNextLevel => Mathf.RoundToInt(baseExpToLevel * Mathf.Pow(expScaling, Level - 1));
        public float HealthPercent => CurrentHealth / MaxHealth;

        // Events
        public event Action<float, float> OnHealthChanged; // current, max
        public event Action<int> OnLevelUp;
        public event Action<int> OnGoldChanged;
        public event Action OnDeath;

        // Stat modifiers
        private Dictionary<StatType, List<StatModifier>> modifiers = new Dictionary<StatType, List<StatModifier>>();

        private void Start()
        {
            InitializeStats();
        }

        private void Update()
        {
            // Health regeneration
            if (CurrentHealth < MaxHealth && HealthRegen > 0)
            {
                Heal(HealthRegen * Time.deltaTime);
            }

            // Update timed modifiers
            UpdateModifiers();
        }

        private void InitializeStats()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                modifiers[stat] = new List<StatModifier>();
            }

            // Apply meta progression bonuses
            ApplyMetaProgressionBonuses();

            CurrentHealth = MaxHealth;
            Level = 1;
            Experience = 0;
            Gold = 0;
        }

        private void ApplyMetaProgressionBonuses()
        {
            var meta = MetaProgressionManager.Instance;
            if (meta == null) return;

            // Apply permanent upgrades
            int healthLevel = meta.GetUpgradeLevel("health_boost");
            if (healthLevel > 0)
            {
                AddModifier(StatType.MaxHealth, new StatModifier(healthLevel * 10f, ModifierType.Flat, "meta_health"));
            }

            int damageLevel = meta.GetUpgradeLevel("damage_boost");
            if (damageLevel > 0)
            {
                AddModifier(StatType.Damage, new StatModifier(damageLevel * 0.05f, ModifierType.Multiplicative, "meta_damage"));
            }
        }

        public void TakeDamage(float damage, Vector3 source)
        {
            if (CurrentHealth <= 0) return;

            // Apply armor reduction
            float actualDamage = CalculateDamageReduction(damage);

            CurrentHealth = Mathf.Max(0, CurrentHealth - actualDamage);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            EventBus.Publish(new PlayerDamagedEvent
            {
                Damage = actualDamage,
                CurrentHealth = CurrentHealth,
                MaxHealth = MaxHealth,
                DamageSource = source
            });

            if (CurrentHealth <= 0)
            {
                Die();
            }
        }

        private float CalculateDamageReduction(float damage)
        {
            // Armor formula: reduction = armor / (armor + 100)
            float reduction = Armor / (Armor + 100f);
            return damage * (1f - reduction);
        }

        public void Heal(float amount)
        {
            if (CurrentHealth <= 0) return;

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);

            if (CurrentHealth != previousHealth)
            {
                OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

                EventBus.Publish(new PlayerHealedEvent
                {
                    Amount = CurrentHealth - previousHealth,
                    CurrentHealth = CurrentHealth,
                    MaxHealth = MaxHealth
                });
            }
        }

        public void AddExperience(int amount)
        {
            Experience += amount;

            while (Experience >= ExpToNextLevel)
            {
                Experience -= ExpToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            Level++;
            OnLevelUp?.Invoke(Level);

            EventBus.Publish(new LevelUpEvent
            {
                NewLevel = Level,
                UpgradeChoices = 3
            });

            Debug.Log($"Level Up! Now level {Level}");
        }

        public void AddGold(int amount)
        {
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
            GameManager.Instance?.AddGold(amount);

            EventBus.Publish(new GoldChangedEvent
            {
                CurrentGold = Gold,
                Change = amount
            });
        }

        public bool SpendGold(int amount)
        {
            if (Gold >= amount)
            {
                Gold -= amount;
                OnGoldChanged?.Invoke(Gold);
                return true;
            }
            return false;
        }

        private void Die()
        {
            OnDeath?.Invoke();

            EventBus.Publish(new PlayerDeathEvent
            {
                DeathPosition = transform.position
            });

            GameManager.Instance?.ChangeState(GameState.GameOver);
        }

        // Stat Modifier System
        public void AddModifier(StatType stat, StatModifier modifier)
        {
            modifiers[stat].Add(modifier);
        }

        public void RemoveModifier(StatType stat, string source)
        {
            modifiers[stat].RemoveAll(m => m.Source == source);
        }

        public void RemoveAllModifiersFromSource(string source)
        {
            foreach (var statModifiers in modifiers.Values)
            {
                statModifiers.RemoveAll(m => m.Source == source);
            }
        }

        private float CalculateStat(float baseValue, StatType stat)
        {
            float flatBonus = 0f;
            float multiplicativeBonus = 1f;

            foreach (var mod in modifiers[stat])
            {
                if (mod.Type == ModifierType.Flat)
                {
                    flatBonus += mod.Value;
                }
                else
                {
                    multiplicativeBonus += mod.Value;
                }
            }

            return (baseValue + flatBonus) * multiplicativeBonus;
        }

        private float GetMultiplier(StatType stat)
        {
            float multiplier = 1f;

            foreach (var mod in modifiers[stat])
            {
                if (mod.Type == ModifierType.Multiplicative)
                {
                    multiplier += mod.Value;
                }
                else
                {
                    multiplier += mod.Value / 100f;
                }
            }

            return multiplier;
        }

        private void UpdateModifiers()
        {
            foreach (var statModifiers in modifiers.Values)
            {
                for (int i = statModifiers.Count - 1; i >= 0; i--)
                {
                    var mod = statModifiers[i];
                    if (mod.Duration > 0)
                    {
                        mod.Duration -= Time.deltaTime;
                        if (mod.Duration <= 0)
                        {
                            statModifiers.RemoveAt(i);
                        }
                    }
                }
            }
        }

        public void ResetStats()
        {
            foreach (var statModifiers in modifiers.Values)
            {
                statModifiers.Clear();
            }

            ApplyMetaProgressionBonuses();
            CurrentHealth = MaxHealth;
            Level = 1;
            Experience = 0;
            Gold = 0;
        }
    }

    public enum StatType
    {
        MaxHealth,
        Damage,
        AttackSpeed,
        CritChance,
        CritDamage,
        Armor,
        HealthRegen,
        MoveSpeed,
        DashSpeed,
        DashCooldown,
        PickupRange,
        ProjectileSpeed,
        ProjectileCount,
        AreaOfEffect,
        Duration,
        Cooldown,
        Luck
    }

    public enum ModifierType
    {
        Flat,
        Multiplicative
    }

    [Serializable]
    public class StatModifier
    {
        public float Value;
        public ModifierType Type;
        public string Source;
        public float Duration; // -1 for permanent

        public StatModifier(float value, ModifierType type, string source, float duration = -1f)
        {
            Value = value;
            Type = type;
            Source = source;
            Duration = duration;
        }
    }
}
