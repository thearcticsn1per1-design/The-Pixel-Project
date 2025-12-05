using UnityEngine;
using System;
using System.Collections.Generic;
using PixelProject.Core;
using PixelProject.Player;

namespace PixelProject.Items
{
    /// <summary>
    /// Manages in-run upgrades that appear on level up.
    /// </summary>
    public class UpgradeSystem : MonoBehaviour
    {
        public static UpgradeSystem Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int upgradeChoices = 3;
        [SerializeField] private bool allowDuplicates = true;
        [SerializeField] private int maxUpgradeLevel = 5;

        [Header("Upgrade Pool")]
        [SerializeField] private List<UpgradeData> availableUpgrades = new List<UpgradeData>();

        private Dictionary<string, int> upgradelevels = new Dictionary<string, int>();
        private List<UpgradeData> currentChoices = new List<UpgradeData>();
        private PlayerStats playerStats;
        private System.Random random;

        public event Action<List<UpgradeData>> OnUpgradeChoicesReady;
        public event Action<UpgradeData> OnUpgradeSelected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            random = new System.Random();
        }

        private void Start()
        {
            playerStats = PlayerController.Instance?.GetComponent<PlayerStats>();

            // Subscribe to level up events
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            GenerateUpgradeChoices();
        }

        public void GenerateUpgradeChoices()
        {
            currentChoices.Clear();

            // Get valid upgrades
            List<UpgradeData> validUpgrades = new List<UpgradeData>();

            foreach (var upgrade in availableUpgrades)
            {
                if (CanOfferUpgrade(upgrade))
                {
                    validUpgrades.Add(upgrade);
                }
            }

            // Weighted random selection
            int choicesToGenerate = Mathf.Min(upgradeChoices, validUpgrades.Count);

            for (int i = 0; i < choicesToGenerate; i++)
            {
                if (validUpgrades.Count == 0) break;

                UpgradeData selected = SelectWeightedRandom(validUpgrades);
                currentChoices.Add(selected);

                if (!allowDuplicates)
                {
                    validUpgrades.Remove(selected);
                }
            }

            // Pause game and show upgrade UI
            GameManager.Instance?.PauseGame();
            OnUpgradeChoicesReady?.Invoke(currentChoices);

            Debug.Log($"Generated {currentChoices.Count} upgrade choices");
        }

        private bool CanOfferUpgrade(UpgradeData upgrade)
        {
            // Check if maxed
            int currentLevel = GetUpgradeLevel(upgrade.upgradeId);
            if (currentLevel >= maxUpgradeLevel) return false;

            // Check requirements
            if (upgrade.requiredUpgradeId != null && GetUpgradeLevel(upgrade.requiredUpgradeId) < upgrade.requiredLevel)
            {
                return false;
            }

            // Check exclusions
            if (upgrade.excludedUpgradeIds != null)
            {
                foreach (var excluded in upgrade.excludedUpgradeIds)
                {
                    if (GetUpgradeLevel(excluded) > 0) return false;
                }
            }

            return true;
        }

        private UpgradeData SelectWeightedRandom(List<UpgradeData> upgrades)
        {
            // Apply luck stat to weights
            float luck = playerStats?.CalculateStat(0, StatType.Luck) ?? 0;

            int totalWeight = 0;
            List<int> adjustedWeights = new List<int>();

            foreach (var upgrade in upgrades)
            {
                int weight = upgrade.weight;

                // Higher rarity gets boosted by luck
                if (luck > 0)
                {
                    float rarityMultiplier = 1f + ((int)upgrade.rarity * luck * 0.1f);
                    weight = Mathf.RoundToInt(weight * rarityMultiplier);
                }

                adjustedWeights.Add(weight);
                totalWeight += weight;
            }

            int randomValue = random.Next(totalWeight);
            int currentWeight = 0;

            for (int i = 0; i < upgrades.Count; i++)
            {
                currentWeight += adjustedWeights[i];
                if (randomValue < currentWeight)
                {
                    return upgrades[i];
                }
            }

            return upgrades[0];
        }

        public void SelectUpgrade(int index)
        {
            if (index < 0 || index >= currentChoices.Count) return;

            UpgradeData selected = currentChoices[index];
            ApplyUpgrade(selected);

            // Resume game
            GameManager.Instance?.ResumeGame();
        }

        public void SelectUpgrade(UpgradeData upgrade)
        {
            ApplyUpgrade(upgrade);
            GameManager.Instance?.ResumeGame();
        }

        private void ApplyUpgrade(UpgradeData upgrade)
        {
            if (playerStats == null) return;

            int currentLevel = GetUpgradeLevel(upgrade.upgradeId);
            int newLevel = currentLevel + 1;

            // Get level data
            UpgradeLevelData levelData = upgrade.GetLevelData(newLevel);
            if (levelData == null) return;

            // Apply stat modifiers
            foreach (var mod in levelData.statModifiers)
            {
                playerStats.AddModifier(mod.statType, new StatModifier(
                    mod.value,
                    mod.modifierType,
                    $"upgrade_{upgrade.upgradeId}"
                ));
            }

            // Update level
            upgradelevels[upgrade.upgradeId] = newLevel;

            EventBus.Publish(new UpgradeSelectedEvent
            {
                UpgradeId = upgrade.upgradeId,
                NewLevel = newLevel
            });

            OnUpgradeSelected?.Invoke(upgrade);

            Debug.Log($"Applied upgrade: {upgrade.upgradeName} (Level {newLevel})");
        }

        public int GetUpgradeLevel(string upgradeId)
        {
            return upgradelevels.TryGetValue(upgradeId, out int level) ? level : 0;
        }

        public void ResetUpgrades()
        {
            // Remove all upgrade modifiers
            if (playerStats != null)
            {
                foreach (var kvp in upgradelevels)
                {
                    playerStats.RemoveAllModifiersFromSource($"upgrade_{kvp.Key}");
                }
            }

            upgradelevels.Clear();
        }

        public void SkipUpgrade()
        {
            // Player chose not to upgrade
            GameManager.Instance?.ResumeGame();
        }

        public void Reroll()
        {
            // Could cost gold or be limited
            GenerateUpgradeChoices();
        }
    }

    /// <summary>
    /// ScriptableObject defining an upgrade.
    /// </summary>
    [CreateAssetMenu(fileName = "New Upgrade", menuName = "Pixel Project/Upgrade Data")]
    public class UpgradeData : ScriptableObject
    {
        [Header("Identity")]
        public string upgradeId;
        public string upgradeName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Rarity")]
        public ItemRarity rarity = ItemRarity.Common;
        public int weight = 10;

        [Header("Level Data")]
        public List<UpgradeLevelData> levels = new List<UpgradeLevelData>();

        [Header("Requirements")]
        public string requiredUpgradeId;
        public int requiredLevel = 1;
        public List<string> excludedUpgradeIds;

        public UpgradeLevelData GetLevelData(int level)
        {
            if (level <= 0 || level > levels.Count) return null;
            return levels[level - 1];
        }

        public string GetDescription(int level)
        {
            UpgradeLevelData data = GetLevelData(level);
            if (data == null) return description;

            string desc = description;
            foreach (var mod in data.statModifiers)
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

    [Serializable]
    public class UpgradeLevelData
    {
        public string levelDescription;
        public List<ItemStatModifier> statModifiers = new List<ItemStatModifier>();
    }
}
