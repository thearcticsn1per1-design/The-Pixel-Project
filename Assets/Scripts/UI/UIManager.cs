using UnityEngine;
using System.Collections.Generic;
using PixelProject.Core;
using PixelProject.Player;
using PixelProject.Items;

namespace PixelProject.UI
{
    /// <summary>
    /// Central manager for all UI elements and screens.
    /// This implementation is UI-framework agnostic and works without Unity UI or TextMeshPro packages.
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

        // Button callbacks - connect these to UI buttons in the inspector
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
    /// Health bar UI component - framework agnostic.
    /// Assign any UI text/image components and use SendMessage or direct references.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Transform fillTransform; // Scale this for fill amount
        [SerializeField] private SpriteRenderer fillSprite; // Optional: for color changes
        [SerializeField] private Gradient healthGradient;

        private PlayerStats playerStats;
        private Vector3 originalScale;

        private void Start()
        {
            if (fillTransform != null)
            {
                originalScale = fillTransform.localScale;
            }

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

            if (fillTransform != null)
            {
                Vector3 scale = originalScale;
                scale.x = originalScale.x * percent;
                fillTransform.localScale = scale;
            }

            if (fillSprite != null && healthGradient != null)
            {
                fillSprite.color = healthGradient.Evaluate(percent);
            }
        }
    }

    /// <summary>
    /// Experience bar UI component - framework agnostic.
    /// </summary>
    public class ExperienceBar : MonoBehaviour
    {
        [SerializeField] private Transform fillTransform;

        private PlayerStats playerStats;
        private Vector3 originalScale;

        private void Start()
        {
            if (fillTransform != null)
            {
                originalScale = fillTransform.localScale;
            }

            playerStats = PlayerController.Instance?.GetComponent<PlayerStats>();

            if (playerStats != null)
            {
                playerStats.OnLevelUp += OnLevelUp;
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
            if (playerStats == null || fillTransform == null) return;

            float percent = playerStats.ExpToNextLevel > 0
                ? (float)playerStats.Experience / playerStats.ExpToNextLevel
                : 0f;

            Vector3 scale = originalScale;
            scale.x = originalScale.x * percent;
            fillTransform.localScale = scale;
        }

        private void OnLevelUp(int newLevel)
        {
            Debug.Log($"Level Up! Now level {newLevel}");
        }
    }

    /// <summary>
    /// Wave display UI component - framework agnostic.
    /// </summary>
    public class WaveDisplay : MonoBehaviour
    {
        public int CurrentWave { get; private set; }
        public int EnemyCount { get; private set; }

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
            CurrentWave = wave;
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (Enemies.EnemySpawner.Instance != null)
            {
                EnemyCount = Enemies.EnemySpawner.Instance.ActiveEnemyCount;
            }
        }
    }

    /// <summary>
    /// Gold display UI component - framework agnostic.
    /// </summary>
    public class GoldDisplay : MonoBehaviour
    {
        public int CurrentGold { get; private set; }

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
            CurrentGold = gold;
        }
    }

    /// <summary>
    /// Weapon display UI component - framework agnostic.
    /// </summary>
    public class WeaponDisplay : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer weaponIconRenderer;

        private Combat.WeaponController weaponController;

        public int CurrentAmmo { get; private set; }
        public int MaxAmmo { get; private set; }
        public bool IsReloading { get; private set; }

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
            if (weaponIconRenderer != null && currentWeapon.Data.weaponSprite != null)
            {
                weaponIconRenderer.sprite = currentWeapon.Data.weaponSprite;
            }

            // Update ammo info
            if (currentWeapon.Data.usesAmmo)
            {
                CurrentAmmo = currentWeapon.CurrentAmmo;
                MaxAmmo = currentWeapon.Data.magazineSize;
            }

            IsReloading = weaponController.IsReloading;
        }
    }
}
