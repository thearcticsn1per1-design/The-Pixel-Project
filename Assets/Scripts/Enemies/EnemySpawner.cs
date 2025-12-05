using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PixelProject.Core;
using PixelProject.Utilities;

namespace PixelProject.Enemies
{
    /// <summary>
    /// Manages enemy spawning with wave-based progression and difficulty scaling.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public static EnemySpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private float spawnRadius = 15f;
        [SerializeField] private float minSpawnDistance = 8f;
        [SerializeField] private float spawnInterval = 0.5f;
        [SerializeField] private int maxActiveEnemies = 50;

        [Header("Wave Settings")]
        [SerializeField] private float timeBetweenWaves = 5f;
        [SerializeField] private bool autoStartWaves = true;

        [Header("Enemy Pool")]
        [SerializeField] private List<EnemyData> availableEnemies = new List<EnemyData>();
        [SerializeField] private List<EnemyData> bossEnemies = new List<EnemyData>();

        [Header("Elite Settings")]
        [SerializeField] private float eliteChance = 0.1f;
        [SerializeField] private float eliteHealthMultiplier = 2f;
        [SerializeField] private float eliteDamageMultiplier = 1.5f;
        [SerializeField] private Color eliteColor = Color.yellow;

        private List<EnemyBase> activeEnemies = new List<EnemyBase>();
        private int currentWave = 0;
        private int enemiesRemainingToSpawn;
        private int enemiesRemainingInWave;
        private bool isSpawning;
        private bool waveInProgress;

        private Transform playerTransform;
        private Camera mainCamera;

        public int CurrentWave => currentWave;
        public int ActiveEnemyCount => activeEnemies.Count;
        public bool WaveInProgress => waveInProgress;

        public event System.Action<int> OnWaveStarted;
        public event System.Action<int> OnWaveCompleted;
        public event System.Action<EnemyBase> OnEnemySpawned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            mainCamera = Camera.main;

            var player = Player.PlayerController.Instance;
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // Subscribe to game events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnRunStarted += OnRunStarted;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnRunStarted -= OnRunStarted;
            }
        }

        private void Update()
        {
            // Clean up null references
            activeEnemies.RemoveAll(e => e == null || !e.IsAlive);

            // Check wave completion
            if (waveInProgress && enemiesRemainingInWave <= 0 && activeEnemies.Count == 0)
            {
                CompleteWave();
            }
        }

        private void OnRunStarted()
        {
            currentWave = 0;
            ClearAllEnemies();

            if (autoStartWaves)
            {
                StartCoroutine(WaveDelay());
            }
        }

        public void StartNextWave()
        {
            currentWave++;
            StartWave(currentWave);
        }

        public void StartWave(int waveNumber)
        {
            currentWave = waveNumber;
            waveInProgress = true;

            int enemyCount = CalculateEnemiesForWave(waveNumber);
            enemiesRemainingToSpawn = enemyCount;
            enemiesRemainingInWave = enemyCount;

            OnWaveStarted?.Invoke(waveNumber);
            GameManager.Instance?.StartWave(waveNumber);

            EventBus.Publish(new WaveStartedEvent
            {
                WaveNumber = waveNumber,
                EnemyCount = enemyCount
            });

            StartCoroutine(SpawnWaveEnemies());

            Debug.Log($"Wave {waveNumber} started with {enemyCount} enemies");
        }

        private int CalculateEnemiesForWave(int wave)
        {
            return GameManager.Instance?.CalculateEnemiesForWave(wave) ?? (5 + wave * 2);
        }

        private IEnumerator SpawnWaveEnemies()
        {
            isSpawning = true;

            // Check for boss wave (every 5 waves)
            if (currentWave % 5 == 0 && bossEnemies.Count > 0)
            {
                yield return new WaitForSeconds(1f);
                SpawnBoss();
            }

            while (enemiesRemainingToSpawn > 0)
            {
                if (activeEnemies.Count < maxActiveEnemies)
                {
                    SpawnEnemy();
                    enemiesRemainingToSpawn--;
                }

                yield return new WaitForSeconds(spawnInterval);
            }

            isSpawning = false;
        }

        private void SpawnEnemy()
        {
            EnemyData enemyData = SelectEnemyForWave();
            if (enemyData == null || enemyData.prefab == null) return;

            Vector2 spawnPos = GetSpawnPosition();

            GameObject enemyObj = ObjectPool.Instance?.Get(enemyData.prefab);
            if (enemyObj == null)
            {
                enemyObj = Instantiate(enemyData.prefab);
            }

            enemyObj.transform.position = spawnPos;

            var enemy = enemyObj.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.Initialize();
                enemy.OnDeath += OnEnemyDeath;

                // Chance for elite enemy
                if (Random.value < eliteChance * (1f + currentWave * 0.01f))
                {
                    MakeElite(enemy);
                }

                activeEnemies.Add(enemy);
                OnEnemySpawned?.Invoke(enemy);

                EventBus.Publish(new EnemySpawnedEvent
                {
                    Enemy = enemyObj,
                    EnemyType = enemyData.enemyId,
                    Position = spawnPos
                });
            }
        }

        private void SpawnBoss()
        {
            if (bossEnemies.Count == 0) return;

            EnemyData bossData = bossEnemies[Random.Range(0, bossEnemies.Count)];
            if (bossData == null || bossData.prefab == null) return;

            Vector2 spawnPos = GetSpawnPosition();

            GameObject bossObj = Instantiate(bossData.prefab, spawnPos, Quaternion.identity);

            var boss = bossObj.GetComponent<EnemyBase>();
            if (boss != null)
            {
                boss.Initialize();
                boss.OnDeath += OnEnemyDeath;
                activeEnemies.Add(boss);

                Debug.Log($"Boss spawned: {bossData.enemyName}");
            }
        }

        private EnemyData SelectEnemyForWave()
        {
            List<EnemyData> validEnemies = new List<EnemyData>();
            int totalWeight = 0;

            foreach (var enemy in availableEnemies)
            {
                if (enemy.minWaveToSpawn <= currentWave && !enemy.isBoss)
                {
                    validEnemies.Add(enemy);
                    totalWeight += enemy.spawnWeight;
                }
            }

            if (validEnemies.Count == 0) return null;

            // Weighted random selection
            int randomValue = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var enemy in validEnemies)
            {
                currentWeight += enemy.spawnWeight;
                if (randomValue < currentWeight)
                {
                    return enemy;
                }
            }

            return validEnemies[0];
        }

        private Vector2 GetSpawnPosition()
        {
            if (playerTransform == null)
            {
                return Random.insideUnitCircle * spawnRadius;
            }

            // Try to find a valid spawn position
            for (int i = 0; i < 10; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(minSpawnDistance, spawnRadius);

                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                Vector2 spawnPos = (Vector2)playerTransform.position + offset;

                // Check if position is off-screen (preferred for spawning)
                if (mainCamera != null)
                {
                    Vector3 viewportPos = mainCamera.WorldToViewportPoint(spawnPos);
                    bool offScreen = viewportPos.x < -0.1f || viewportPos.x > 1.1f ||
                                     viewportPos.y < -0.1f || viewportPos.y > 1.1f;

                    if (offScreen)
                    {
                        return spawnPos;
                    }
                }
            }

            // Fallback to random position in spawn radius
            float fallbackAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 fallbackOffset = new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle)) * spawnRadius;
            return (Vector2)playerTransform.position + fallbackOffset;
        }

        private void MakeElite(EnemyBase enemy)
        {
            // Apply elite modifiers
            // This would require modifying the enemy stats
            var spriteRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = eliteColor;
            }

            // Scale up slightly
            enemy.transform.localScale *= 1.2f;

            Debug.Log($"Elite enemy spawned!");
        }

        private void OnEnemyDeath(EnemyBase enemy)
        {
            enemy.OnDeath -= OnEnemyDeath;
            activeEnemies.Remove(enemy);
            enemiesRemainingInWave--;
        }

        private void CompleteWave()
        {
            waveInProgress = false;

            OnWaveCompleted?.Invoke(currentWave);
            GameManager.Instance?.CompleteWave();

            EventBus.Publish(new WaveCompletedEvent
            {
                WaveNumber = currentWave,
                TimeTaken = Time.time
            });

            Debug.Log($"Wave {currentWave} completed!");

            if (autoStartWaves)
            {
                StartCoroutine(WaveDelay());
            }
        }

        private IEnumerator WaveDelay()
        {
            yield return new WaitForSeconds(timeBetweenWaves);

            if (GameManager.Instance?.CurrentState == GameState.Playing)
            {
                StartNextWave();
            }
        }

        public void ClearAllEnemies()
        {
            StopAllCoroutines();
            isSpawning = false;
            waveInProgress = false;

            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }

            activeEnemies.Clear();
        }

        public void AddEnemyToPool(EnemyData enemyData)
        {
            if (!availableEnemies.Contains(enemyData))
            {
                availableEnemies.Add(enemyData);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, minSpawnDistance);
        }
    }
}
