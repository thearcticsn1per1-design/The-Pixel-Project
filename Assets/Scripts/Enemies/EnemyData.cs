using UnityEngine;

namespace PixelProject.Enemies
{
    /// <summary>
    /// ScriptableObject defining enemy properties and behavior parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "New Enemy", menuName = "Pixel Project/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyId;
        public string enemyName;
        [TextArea] public string description;
        public Sprite sprite;

        [Header("Stats")]
        public float maxHealth = 50f;
        public float damage = 10f;
        public float contactDamage = 5f;
        public float armor = 0f;
        public float moveSpeed = 3f;
        public float knockbackResistance = 1f;

        [Header("Combat")]
        public float attackRange = 1.5f;
        public float attackCooldown = 1f;
        public float detectionRange = 10f;

        [Header("Loot")]
        public int goldDrop = 5;
        public int experienceDrop = 10;
        public float itemDropChance = 0.1f;

        [Header("Spawning")]
        public EnemyType enemyType = EnemyType.Basic;
        public int spawnWeight = 10;
        public int minWaveToSpawn = 1;
        public bool isBoss = false;

        [Header("Prefab")]
        public GameObject prefab;

        [Header("Audio")]
        public AudioClip spawnSound;
        public AudioClip attackSound;
        public AudioClip deathSound;
    }

    public enum EnemyType
    {
        Basic,      // Simple chase AI
        Ranged,     // Keeps distance and shoots
        Charger,    // Charges at player
        Tank,       // Slow, high HP
        Swarm,      // Spawns in groups
        Elite,      // Enhanced version of basic types
        Boss        // Boss enemy
    }
}
