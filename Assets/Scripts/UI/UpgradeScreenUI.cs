using UnityEngine;
using System.Collections.Generic;
using PixelProject.Items;

namespace PixelProject.UI
{
    /// <summary>
    /// UI controller for the upgrade selection screen.
    /// Framework-agnostic implementation that works without Unity UI or TextMeshPro.
    /// </summary>
    public class UpgradeScreenUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform upgradeCardsParent;
        [SerializeField] private GameObject upgradeCardPrefab;

        [Header("Settings")]
        [SerializeField] private int rerollCost = 50;
        [SerializeField] private float rerollCostIncrease = 1.5f;

        private List<UpgradeCardUI> activeCards = new List<UpgradeCardUI>();
        private List<UpgradeData> currentUpgrades;
        private int currentRerollCost;

        public int CurrentRerollCost => currentRerollCost;

        private void OnEnable()
        {
            currentRerollCost = rerollCost;
        }

        public void DisplayUpgrades(List<UpgradeData> upgrades)
        {
            currentUpgrades = upgrades;

            // Clear existing cards
            foreach (var card in activeCards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }
            activeCards.Clear();

            // Create new cards
            for (int i = 0; i < upgrades.Count; i++)
            {
                CreateUpgradeCard(upgrades[i], i);
            }
        }

        private void CreateUpgradeCard(UpgradeData upgrade, int index)
        {
            if (upgradeCardPrefab == null || upgradeCardsParent == null) return;

            GameObject cardObj = Instantiate(upgradeCardPrefab, upgradeCardsParent);
            UpgradeCardUI card = cardObj.GetComponent<UpgradeCardUI>();

            if (card != null)
            {
                int currentLevel = UpgradeSystem.Instance?.GetUpgradeLevel(upgrade.upgradeId) ?? 0;
                card.Setup(upgrade, currentLevel + 1, index, OnUpgradeSelected);
                activeCards.Add(card);
            }
        }

        private void OnUpgradeSelected(int index)
        {
            UpgradeSystem.Instance?.SelectUpgrade(index);
            UIManager.Instance?.HideUpgradeScreen();
        }

        // Call this from a button
        public void OnSkipClicked()
        {
            UpgradeSystem.Instance?.SkipUpgrade();
            UIManager.Instance?.HideUpgradeScreen();
        }

        // Call this from a button
        public void OnRerollClicked()
        {
            var playerStats = Player.PlayerController.Instance?.GetComponent<Player.PlayerStats>();
            if (playerStats == null) return;

            if (playerStats.SpendGold(currentRerollCost))
            {
                currentRerollCost = Mathf.RoundToInt(currentRerollCost * rerollCostIncrease);
                UpgradeSystem.Instance?.Reroll();
            }
        }

        public bool CanAffordReroll()
        {
            var playerStats = Player.PlayerController.Instance?.GetComponent<Player.PlayerStats>();
            return playerStats != null && playerStats.Gold >= currentRerollCost;
        }
    }

    /// <summary>
    /// Individual upgrade card UI element.
    /// Framework-agnostic - use SpriteRenderers for icons and expose data for custom UI.
    /// </summary>
    public class UpgradeCardUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private SpriteRenderer borderRenderer;

        [Header("Rarity Colors")]
        [SerializeField] private Color commonColor = Color.gray;
        [SerializeField] private Color uncommonColor = Color.green;
        [SerializeField] private Color rareColor = Color.blue;
        [SerializeField] private Color epicColor = Color.magenta;
        [SerializeField] private Color legendaryColor = Color.yellow;

        private UpgradeData upgradeData;
        private int cardIndex;
        private System.Action<int> onSelectCallback;

        // Public properties for custom UI bindings
        public UpgradeData Data => upgradeData;
        public string UpgradeName => upgradeData?.upgradeName ?? "";
        public string Description { get; private set; }
        public int Level { get; private set; }

        public void Setup(UpgradeData upgrade, int level, int index, System.Action<int> callback)
        {
            upgradeData = upgrade;
            cardIndex = index;
            onSelectCallback = callback;
            Level = level;
            Description = upgrade?.GetDescription(level) ?? "";

            // Set icon
            if (iconRenderer != null && upgrade?.icon != null)
            {
                iconRenderer.sprite = upgrade.icon;
            }

            // Set rarity color
            SetRarityColor(upgrade?.rarity ?? Core.ItemRarity.Common);
        }

        private void SetRarityColor(Core.ItemRarity rarity)
        {
            Color color = rarity switch
            {
                Core.ItemRarity.Common => commonColor,
                Core.ItemRarity.Uncommon => uncommonColor,
                Core.ItemRarity.Rare => rareColor,
                Core.ItemRarity.Epic => epicColor,
                Core.ItemRarity.Legendary => legendaryColor,
                _ => commonColor
            };

            if (borderRenderer != null)
            {
                borderRenderer.color = color;
            }
        }

        // Call this from a button or click handler
        public void OnSelectClicked()
        {
            onSelectCallback?.Invoke(cardIndex);
        }

        // For mouse click detection if not using UI buttons
        private void OnMouseDown()
        {
            OnSelectClicked();
        }
    }

    /// <summary>
    /// Floating damage number - framework agnostic, uses SpriteRenderer or TextMesh.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMesh damageTextMesh; // Use 3D TextMesh instead of UI Text
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float fadeStartTime = 0.5f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color criticalColor = Color.yellow;
        [SerializeField] private Color healColor = Color.green;

        private float timer;
        private Vector3 startPosition;
        private Color currentColor;

        public void Initialize(float damage, bool isCritical = false, bool isHeal = false)
        {
            if (damageTextMesh != null)
            {
                damageTextMesh.text = isHeal ? $"+{damage:F0}" : damage.ToString("F0");

                if (isHeal)
                {
                    currentColor = healColor;
                }
                else if (isCritical)
                {
                    currentColor = criticalColor;
                    damageTextMesh.characterSize *= 1.5f;
                    damageTextMesh.text += "!";
                }
                else
                {
                    currentColor = normalColor;
                }

                damageTextMesh.color = currentColor;
            }

            startPosition = transform.position;
            timer = 0f;
        }

        private void Update()
        {
            timer += Time.deltaTime;

            // Float upward
            transform.position = startPosition + Vector3.up * (floatSpeed * timer);

            // Fade out
            if (timer >= fadeStartTime && damageTextMesh != null)
            {
                float fadeProgress = (timer - fadeStartTime) / (lifetime - fadeStartTime);
                Color color = currentColor;
                color.a = 1f - fadeProgress;
                damageTextMesh.color = color;
            }

            // Destroy
            if (timer >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Game over screen UI controller - framework agnostic.
    /// Exposes data properties for custom UI bindings.
    /// </summary>
    public class GameOverScreenUI : MonoBehaviour
    {
        // Public properties that can be read by custom UI systems
        public bool IsVictory { get; private set; }
        public int WaveReached { get; private set; }
        public int EnemiesKilled { get; private set; }
        public int GoldCollected { get; private set; }
        public float RunTime { get; private set; }
        public string FormattedTime { get; private set; }

        private void OnEnable()
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            var gameManager = Core.GameManager.Instance;
            if (gameManager == null) return;

            IsVictory = gameManager.CurrentState == Core.GameState.Victory;
            WaveReached = gameManager.CurrentWave;
            EnemiesKilled = gameManager.EnemiesKilled;
            GoldCollected = gameManager.GoldCollected;
            RunTime = gameManager.RunTime;

            int minutes = Mathf.FloorToInt(RunTime / 60f);
            int seconds = Mathf.FloorToInt(RunTime % 60f);
            FormattedTime = $"{minutes:00}:{seconds:00}";
        }
    }
}
