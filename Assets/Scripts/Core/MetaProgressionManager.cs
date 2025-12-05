using UnityEngine;
using System;
using System.Collections.Generic;

namespace PixelProject.Core
{
    /// <summary>
    /// Manages persistent progression across runs including currencies,
    /// unlocks, and permanent upgrades.
    /// </summary>
    public class MetaProgressionManager : MonoBehaviour
    {
        public static MetaProgressionManager Instance { get; private set; }

        private const string SAVE_KEY = "MetaProgression";

        [Header("Meta Currency")]
        [SerializeField] private int startingSouls = 0;

        public int Souls { get; private set; }
        public int TotalRuns { get; private set; }
        public int TotalKills { get; private set; }
        public int HighestWave { get; private set; }
        public int Victories { get; private set; }

        public event Action<int> OnSoulsChanged;
        public event Action<string> OnUnlockAchieved;

        private HashSet<string> unlockedItems = new HashSet<string>();
        private HashSet<string> unlockedUpgrades = new HashSet<string>();
        private HashSet<string> achievements = new HashSet<string>();
        private Dictionary<string, int> permanentUpgradeLevels = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
        }

        public void ProcessRunEnd(RunSummary summary)
        {
            TotalRuns++;
            TotalKills += summary.EnemiesKilled;

            if (summary.WavesCompleted > HighestWave)
            {
                HighestWave = summary.WavesCompleted;
            }

            if (summary.Victory)
            {
                Victories++;
            }

            // Convert gold to souls (meta currency)
            int soulsEarned = CalculateSoulsEarned(summary);
            AddSouls(soulsEarned);

            CheckAchievements(summary);
            SaveProgress();

            Debug.Log($"Run processed. Earned {soulsEarned} souls. Total souls: {Souls}");
        }

        private int CalculateSoulsEarned(RunSummary summary)
        {
            int souls = summary.GoldCollected / 10; // Base conversion
            souls += summary.WavesCompleted * 5;    // Wave bonus
            souls += summary.EnemiesKilled / 10;    // Kill bonus

            if (summary.Victory)
            {
                souls *= 2; // Victory multiplier
            }

            return souls;
        }

        public void AddSouls(int amount)
        {
            Souls += amount;
            OnSoulsChanged?.Invoke(Souls);
        }

        public bool SpendSouls(int amount)
        {
            if (Souls >= amount)
            {
                Souls -= amount;
                OnSoulsChanged?.Invoke(Souls);
                SaveProgress();
                return true;
            }
            return false;
        }

        public bool UnlockItem(string itemId, int cost)
        {
            if (unlockedItems.Contains(itemId)) return false;
            if (!SpendSouls(cost)) return false;

            unlockedItems.Add(itemId);
            OnUnlockAchieved?.Invoke(itemId);
            SaveProgress();

            Debug.Log($"Unlocked item: {itemId}");
            return true;
        }

        public bool IsItemUnlocked(string itemId)
        {
            return unlockedItems.Contains(itemId);
        }

        public bool PurchaseUpgrade(string upgradeId, int cost)
        {
            if (!SpendSouls(cost)) return false;

            if (!permanentUpgradeLevels.ContainsKey(upgradeId))
            {
                permanentUpgradeLevels[upgradeId] = 0;
            }

            permanentUpgradeLevels[upgradeId]++;
            unlockedUpgrades.Add(upgradeId);
            SaveProgress();

            Debug.Log($"Purchased upgrade: {upgradeId} (Level {permanentUpgradeLevels[upgradeId]})");
            return true;
        }

        public int GetUpgradeLevel(string upgradeId)
        {
            return permanentUpgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;
        }

        public bool HasAchievement(string achievementId)
        {
            return achievements.Contains(achievementId);
        }

        private void CheckAchievements(RunSummary summary)
        {
            // First Run
            if (TotalRuns == 1 && !achievements.Contains("first_run"))
            {
                UnlockAchievement("first_run");
            }

            // First Victory
            if (summary.Victory && Victories == 1 && !achievements.Contains("first_victory"))
            {
                UnlockAchievement("first_victory");
            }

            // Wave Milestones
            if (summary.WavesCompleted >= 10 && !achievements.Contains("wave_10"))
            {
                UnlockAchievement("wave_10");
            }

            if (summary.WavesCompleted >= 25 && !achievements.Contains("wave_25"))
            {
                UnlockAchievement("wave_25");
            }

            // Kill Milestones
            if (TotalKills >= 100 && !achievements.Contains("kills_100"))
            {
                UnlockAchievement("kills_100");
            }

            if (TotalKills >= 1000 && !achievements.Contains("kills_1000"))
            {
                UnlockAchievement("kills_1000");
            }
        }

        private void UnlockAchievement(string achievementId)
        {
            achievements.Add(achievementId);
            OnUnlockAchieved?.Invoke($"achievement_{achievementId}");
            Debug.Log($"Achievement unlocked: {achievementId}");
        }

        public void SaveProgress()
        {
            var data = new MetaSaveData
            {
                Souls = Souls,
                TotalRuns = TotalRuns,
                TotalKills = TotalKills,
                HighestWave = HighestWave,
                Victories = Victories,
                UnlockedItems = new List<string>(unlockedItems),
                UnlockedUpgrades = new List<string>(unlockedUpgrades),
                Achievements = new List<string>(achievements),
                UpgradeLevels = new List<UpgradeLevelEntry>()
            };

            foreach (var kvp in permanentUpgradeLevels)
            {
                data.UpgradeLevels.Add(new UpgradeLevelEntry { Id = kvp.Key, Level = kvp.Value });
            }

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
        }

        public void LoadProgress()
        {
            if (PlayerPrefs.HasKey(SAVE_KEY))
            {
                string json = PlayerPrefs.GetString(SAVE_KEY);
                var data = JsonUtility.FromJson<MetaSaveData>(json);

                Souls = data.Souls;
                TotalRuns = data.TotalRuns;
                TotalKills = data.TotalKills;
                HighestWave = data.HighestWave;
                Victories = data.Victories;

                unlockedItems = new HashSet<string>(data.UnlockedItems ?? new List<string>());
                unlockedUpgrades = new HashSet<string>(data.UnlockedUpgrades ?? new List<string>());
                achievements = new HashSet<string>(data.Achievements ?? new List<string>());

                permanentUpgradeLevels.Clear();
                if (data.UpgradeLevels != null)
                {
                    foreach (var entry in data.UpgradeLevels)
                    {
                        permanentUpgradeLevels[entry.Id] = entry.Level;
                    }
                }

                Debug.Log($"Progress loaded. Souls: {Souls}, Runs: {TotalRuns}");
            }
            else
            {
                Souls = startingSouls;
                Debug.Log("No save data found. Starting fresh.");
            }
        }

        public void ResetProgress()
        {
            PlayerPrefs.DeleteKey(SAVE_KEY);
            Souls = startingSouls;
            TotalRuns = 0;
            TotalKills = 0;
            HighestWave = 0;
            Victories = 0;
            unlockedItems.Clear();
            unlockedUpgrades.Clear();
            achievements.Clear();
            permanentUpgradeLevels.Clear();

            OnSoulsChanged?.Invoke(Souls);
            Debug.Log("Progress reset.");
        }
    }

    [Serializable]
    public class MetaSaveData
    {
        public int Souls;
        public int TotalRuns;
        public int TotalKills;
        public int HighestWave;
        public int Victories;
        public List<string> UnlockedItems;
        public List<string> UnlockedUpgrades;
        public List<string> Achievements;
        public List<UpgradeLevelEntry> UpgradeLevels;
    }

    [Serializable]
    public class UpgradeLevelEntry
    {
        public string Id;
        public int Level;
    }
}
