using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using PixelProject.Items;

namespace PixelProject.UI
{
    /// <summary>
    /// UI controller for the upgrade selection screen.
    /// </summary>
    public class UpgradeScreenUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform upgradeCardsParent;
        [SerializeField] private GameObject upgradeCardPrefab;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Text rerollCostText;

        [Header("Settings")]
        [SerializeField] private int rerollCost = 50;
        [SerializeField] private float rerollCostIncrease = 1.5f;

        private List<UpgradeCardUI> activeCards = new List<UpgradeCardUI>();
        private List<UpgradeData> currentUpgrades;
        private int currentRerollCost;

        private void OnEnable()
        {
            currentRerollCost = rerollCost;
            UpdateRerollButton();
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

        public void OnSkipClicked()
        {
            UpgradeSystem.Instance?.SkipUpgrade();
            UIManager.Instance?.HideUpgradeScreen();
        }

        public void OnRerollClicked()
        {
            var playerStats = Player.PlayerController.Instance?.GetComponent<Player.PlayerStats>();
            if (playerStats == null) return;

            if (playerStats.SpendGold(currentRerollCost))
            {
                currentRerollCost = Mathf.RoundToInt(currentRerollCost * rerollCostIncrease);
                UpdateRerollButton();

                UpgradeSystem.Instance?.Reroll();
            }
        }

        private void UpdateRerollButton()
        {
            if (rerollCostText != null)
            {
                rerollCostText.text = $"Reroll ({currentRerollCost}g)";
            }

            // Disable if not enough gold
            if (rerollButton != null)
            {
                var playerStats = Player.PlayerController.Instance?.GetComponent<Player.PlayerStats>();
                rerollButton.interactable = playerStats != null && playerStats.Gold >= currentRerollCost;
            }
        }
    }

    /// <summary>
    /// Individual upgrade card UI element.
    /// </summary>
    public class UpgradeCardUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button selectButton;

        [Header("Rarity Colors")]
        [SerializeField] private Color commonColor = Color.gray;
        [SerializeField] private Color uncommonColor = Color.green;
        [SerializeField] private Color rareColor = Color.blue;
        [SerializeField] private Color epicColor = Color.magenta;
        [SerializeField] private Color legendaryColor = Color.yellow;

        private UpgradeData upgradeData;
        private int cardIndex;
        private System.Action<int> onSelectCallback;

        public void Setup(UpgradeData upgrade, int level, int index, System.Action<int> callback)
        {
            upgradeData = upgrade;
            cardIndex = index;
            onSelectCallback = callback;

            // Set icon
            if (iconImage != null && upgrade.icon != null)
            {
                iconImage.sprite = upgrade.icon;
            }

            // Set name
            if (nameText != null)
            {
                nameText.text = upgrade.upgradeName;
            }

            // Set level
            if (levelText != null)
            {
                levelText.text = $"Level {level}";
            }

            // Set description
            if (descriptionText != null)
            {
                descriptionText.text = upgrade.GetDescription(level);
            }

            // Set rarity color
            SetRarityColor(upgrade.rarity);

            // Setup button
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClicked);
            }
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

            if (borderImage != null)
            {
                borderImage.color = color;
            }
        }

        private void OnSelectClicked()
        {
            onSelectCallback?.Invoke(cardIndex);
        }
    }

    /// <summary>
    /// Floating damage number UI element.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private Text damageText;
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float fadeStartTime = 0.5f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color criticalColor = Color.yellow;
        [SerializeField] private Color healColor = Color.green;

        private float timer;
        private Vector3 startPosition;

        public void Initialize(float damage, bool isCritical = false, bool isHeal = false)
        {
            if (damageText != null)
            {
                damageText.text = isHeal ? $"+{damage:F0}" : damage.ToString("F0");

                if (isHeal)
                {
                    damageText.color = healColor;
                }
                else if (isCritical)
                {
                    damageText.color = criticalColor;
                    damageText.fontSize = Mathf.RoundToInt(damageText.fontSize * 1.5f);
                    damageText.text += "!";
                }
                else
                {
                    damageText.color = normalColor;
                }
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
            if (timer >= fadeStartTime && damageText != null)
            {
                float fadeProgress = (timer - fadeStartTime) / (lifetime - fadeStartTime);
                Color color = damageText.color;
                color.a = 1f - fadeProgress;
                damageText.color = color;
            }

            // Destroy
            if (timer >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Game over screen UI controller.
    /// </summary>
    public class GameOverScreenUI : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text killsText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text soulsEarnedText;

        private void OnEnable()
        {
            DisplayStats();
        }

        private void DisplayStats()
        {
            var gameManager = Core.GameManager.Instance;
            if (gameManager == null) return;

            if (titleText != null)
            {
                titleText.text = gameManager.CurrentState == Core.GameState.Victory ? "Victory!" : "Game Over";
            }

            if (waveText != null)
            {
                waveText.text = $"Wave Reached: {gameManager.CurrentWave}";
            }

            if (killsText != null)
            {
                killsText.text = $"Enemies Killed: {gameManager.EnemiesKilled}";
            }

            if (goldText != null)
            {
                goldText.text = $"Gold Collected: {gameManager.GoldCollected}";
            }

            if (timeText != null)
            {
                float time = gameManager.RunTime;
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                timeText.text = $"Time: {minutes:00}:{seconds:00}";
            }
        }
    }
}
