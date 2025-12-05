using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using PixelProject.Core;
using PixelProject.Player;
using PixelProject.Items;

namespace PixelProject.UI
{
    /// <summary>
    /// Central manager for all UI elements and screens.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Screen References")]
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject gameHUD;
        [SerializeField] private GameObject pauseScreen;
        [SerializeField] private GameObject upgradeScreen;
        [SerializeField] private GameObject gameOverScreen;
        [SerializeField] private GameObject victoryScreen;

        [Header("HUD Components")]
        [SerializeField] private HealthBar healthBar;
        [SerializeField] private ExperienceBar expBar;
        [SerializeField] private WaveDisplay waveDisplay;
        [SerializeField] private GoldDisplay goldDisplay;
        [SerializeField] private WeaponDisplay weaponDisplay;
        [SerializeField] private TMP_Text timerText;

        private GameState currentScreenState;
        private List<GameObject> allScreens = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Collect all screens
            if (mainMenuScreen != null) allScreens.Add(mainMenuScreen);
            if (gameHUD != null) allScreens.Add(gameHUD);
            if (pauseScreen != null) allScreens.Add(pauseScreen);
            if (upgradeScreen != null) allScreens.Add(upgradeScreen);
            if (gameOverScreen != null) allScreens.Add(gameOverScreen);
            if (victoryScreen != null) allScreens.Add(victoryScreen);
        }

        private void Start()
        {
            // Subscribe to events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
            }

            if (UpgradeSystem.Instance != null)
            {
                UpgradeSystem.Instance.OnUpgradeChoicesReady += ShowUpgradeScreen;
            }

            // Initialize to current state
            OnGameStateChanged(GameManager.Instance?.CurrentState ?? GameState.MainMenu);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }

            if (UpgradeSystem.Instance != null)
            {
                UpgradeSystem.Instance.OnUpgradeChoicesReady -= ShowUpgradeScreen;
            }
        }

        private void Update()
        {
            // Update timer
            if (timerText != null && GameManager.Instance != null)
            {
                float time = GameManager.Instance.RunTime;
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }

            // Escape key handling
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeKey();
            }
        }

        private void OnGameStateChanged(GameState newState)
        {
            currentScreenState = newState;
            HideAllScreens();

            switch (newState)
            {
                case GameState.MainMenu:
                    ShowScreen(mainMenuScreen);
                    break;

                case GameState.Playing:
                    ShowScreen(gameHUD);
                    break;

                case GameState.Paused:
                    ShowScreen(gameHUD);
                    ShowScreen(pauseScreen);
                    break;

                case GameState.GameOver:
                    ShowScreen(gameOverScreen);
                    break;

                case GameState.Victory:
                    ShowScreen(victoryScreen);
                    break;
            }
        }

        private void HandleEscapeKey()
        {
            switch (currentScreenState)
            {
                case GameState.Playing:
                    GameManager.Instance?.PauseGame();
                    break;

                case GameState.Paused:
                    GameManager.Instance?.ResumeGame();
                    break;
            }
        }

        private void HideAllScreens()
        {
            foreach (var screen in allScreens)
            {
                if (screen != null)
                {
                    screen.SetActive(false);
                }
            }
        }

        private void ShowScreen(GameObject screen)
        {
            if (screen != null)
            {
                screen.SetActive(true);
            }
        }

        private void ShowUpgradeScreen(List<UpgradeData> upgrades)
        {
            ShowScreen(upgradeScreen);

            var upgradeUI = upgradeScreen?.GetComponent<UpgradeScreenUI>();
            if (upgradeUI != null)
            {
                upgradeUI.DisplayUpgrades(upgrades);
            }
        }

        public void HideUpgradeScreen()
        {
            if (upgradeScreen != null)
            {
                upgradeScreen.SetActive(false);
            }
        }

        // Button callbacks
        public void OnStartGameClicked()
        {
            GameManager.Instance?.StartNewRun();
        }

        public void OnResumeClicked()
        {
            GameManager.Instance?.ResumeGame();
        }

        public void OnMainMenuClicked()
        {
            GameManager.Instance?.ReturnToMainMenu();
        }

        public void OnQuitClicked()
        {
            GameManager.Instance?.QuitGame();
        }

        public void OnRetryClicked()
        {
            GameManager.Instance?.StartNewRun();
        }
    }

    /// <summary>
    /// Health bar UI component.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Gradient healthGradient;

        private PlayerStats playerStats;

        private void Start()
        {
            playerStats = PlayerController.Instance?.GetComponent<PlayerStats>();

            if (playerStats != null)
            {
                playerStats.OnHealthChanged += UpdateDisplay;
                UpdateDisplay(playerStats.CurrentHealth, playerStats.MaxHealth);
            }
        }

        private void OnDestroy()
        {
            if (playerStats != null)
            {
                playerStats.OnHealthChanged -= UpdateDisplay;
            }
        }

        private void UpdateDisplay(float current, float max)
        {
            float percent = max > 0 ? current / max : 0f;

            if (fillImage != null)
            {
                fillImage.fillAmount = percent;

                if (healthGradient != null)
                {
                    fillImage.color = healthGradient.Evaluate(percent);
                }
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }
    }

    /// <summary>
    /// Experience bar UI component.
    /// </summary>
    public class ExperienceBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text levelText;

        private PlayerStats playerStats;

        private void Start()
        {
            playerStats = PlayerController.Instance?.GetComponent<PlayerStats>();

            if (playerStats != null)
            {
                playerStats.OnLevelUp += OnLevelUp;
                UpdateLevelText(playerStats.Level);
            }
        }

        private void OnDestroy()
        {
            if (playerStats != null)
            {
                playerStats.OnLevelUp -= OnLevelUp;
            }
        }

        private void Update()
        {
            if (playerStats == null || fillImage == null) return;

            float percent = playerStats.ExpToNextLevel > 0
                ? (float)playerStats.Experience / playerStats.ExpToNextLevel
                : 0f;

            fillImage.fillAmount = percent;
        }

        private void OnLevelUp(int newLevel)
        {
            UpdateLevelText(newLevel);
        }

        private void UpdateLevelText(int level)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv. {level}";
            }
        }
    }

    /// <summary>
    /// Wave display UI component.
    /// </summary>
    public class WaveDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text enemiesText;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnWaveStarted += OnWaveStarted;
            }

            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnWaveStarted -= OnWaveStarted;
            }

            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnWaveStarted(int wave)
        {
            if (waveText != null)
            {
                waveText.text = $"Wave {wave}";
            }
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            UpdateEnemyCount();
        }

        private void UpdateEnemyCount()
        {
            if (enemiesText != null && Enemies.EnemySpawner.Instance != null)
            {
                enemiesText.text = $"Enemies: {Enemies.EnemySpawner.Instance.ActiveEnemyCount}";
            }
        }
    }

    /// <summary>
    /// Gold display UI component.
    /// </summary>
    public class GoldDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text goldText;

        private PlayerStats playerStats;

        private void Start()
        {
            playerStats = PlayerController.Instance?.GetComponent<PlayerStats>();

            if (playerStats != null)
            {
                playerStats.OnGoldChanged += UpdateDisplay;
                UpdateDisplay(playerStats.Gold);
            }
        }

        private void OnDestroy()
        {
            if (playerStats != null)
            {
                playerStats.OnGoldChanged -= UpdateDisplay;
            }
        }

        private void UpdateDisplay(int gold)
        {
            if (goldText != null)
            {
                goldText.text = gold.ToString();
            }
        }
    }

    /// <summary>
    /// Weapon display UI component.
    /// </summary>
    public class WeaponDisplay : MonoBehaviour
    {
        [SerializeField] private Image weaponIcon;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private Image reloadBar;

        private Combat.WeaponController weaponController;

        private void Start()
        {
            weaponController = PlayerController.Instance?.GetComponentInChildren<Combat.WeaponController>();
        }

        private void Update()
        {
            if (weaponController == null) return;

            var currentWeapon = weaponController.CurrentWeapon;
            if (currentWeapon == null) return;

            // Update icon
            if (weaponIcon != null && currentWeapon.Data.iconSprite != null)
            {
                weaponIcon.sprite = currentWeapon.Data.iconSprite;
            }

            // Update ammo
            if (ammoText != null && currentWeapon.Data.usesAmmo)
            {
                ammoText.text = $"{currentWeapon.CurrentAmmo} / {currentWeapon.Data.magazineSize}";
            }

            // Update reload bar
            if (reloadBar != null)
            {
                reloadBar.gameObject.SetActive(weaponController.IsReloading);
            }
        }
    }
}
