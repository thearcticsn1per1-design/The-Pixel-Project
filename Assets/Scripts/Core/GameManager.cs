using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace PixelProject.Core
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory,
        Loading
    }

    /// <summary>
    /// Central game manager handling game state, run management, and core game flow.
    /// Implements singleton pattern for global access.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Settings")]
        [SerializeField] private float difficultyScaling = 0.1f;
        [SerializeField] private int baseEnemiesPerWave = 5;

        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public int CurrentRunNumber { get; private set; }
        public int CurrentWave { get; private set; }
        public float RunTime { get; private set; }
        public int EnemiesKilled { get; private set; }
        public int GoldCollected { get; private set; }

        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnWaveStarted;
        public event Action<int> OnWaveCompleted;
        public event Action OnRunStarted;
        public event Action<RunSummary> OnRunEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            CurrentRunNumber = MetaProgressionManager.Instance?.TotalRuns ?? 0;
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing)
            {
                RunTime += Time.deltaTime;
            }
        }

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState previousState = CurrentState;
            CurrentState = newState;

            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                case GameState.Victory:
                    EndRun(newState == GameState.Victory);
                    break;
            }

            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"Game state changed from {previousState} to {newState}");
        }

        public void StartNewRun()
        {
            CurrentRunNumber++;
            CurrentWave = 0;
            RunTime = 0f;
            EnemiesKilled = 0;
            GoldCollected = 0;

            ChangeState(GameState.Playing);
            OnRunStarted?.Invoke();
            StartWave(1);

            Debug.Log($"Starting run #{CurrentRunNumber}");
        }

        public void StartWave(int waveNumber)
        {
            CurrentWave = waveNumber;
            int enemyCount = CalculateEnemiesForWave(waveNumber);

            OnWaveStarted?.Invoke(waveNumber);
            Debug.Log($"Wave {waveNumber} started with {enemyCount} enemies");
        }

        public void CompleteWave()
        {
            OnWaveCompleted?.Invoke(CurrentWave);
            Debug.Log($"Wave {CurrentWave} completed!");
        }

        public int CalculateEnemiesForWave(int wave)
        {
            return Mathf.RoundToInt(baseEnemiesPerWave * (1f + (wave - 1) * difficultyScaling));
        }

        public float GetDifficultyMultiplier()
        {
            return 1f + (CurrentWave - 1) * difficultyScaling;
        }

        public void RegisterEnemyKill()
        {
            EnemiesKilled++;
        }

        public void AddGold(int amount)
        {
            GoldCollected += amount;
        }

        private void EndRun(bool victory)
        {
            Time.timeScale = 0f;

            var summary = new RunSummary
            {
                RunNumber = CurrentRunNumber,
                WavesCompleted = CurrentWave,
                EnemiesKilled = EnemiesKilled,
                GoldCollected = GoldCollected,
                RunDuration = RunTime,
                Victory = victory
            };

            MetaProgressionManager.Instance?.ProcessRunEnd(summary);
            OnRunEnded?.Invoke(summary);

            Debug.Log($"Run #{CurrentRunNumber} ended. Victory: {victory}, Waves: {CurrentWave}, Kills: {EnemiesKilled}");
        }

        public void PauseGame()
        {
            if (CurrentState == GameState.Playing)
            {
                ChangeState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                ChangeState(GameState.Playing);
            }
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            ChangeState(GameState.MainMenu);
            SceneManager.LoadScene("MainMenu");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    [Serializable]
    public struct RunSummary
    {
        public int RunNumber;
        public int WavesCompleted;
        public int EnemiesKilled;
        public int GoldCollected;
        public float RunDuration;
        public bool Victory;
    }
}
