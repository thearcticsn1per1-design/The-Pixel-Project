using UnityEngine;
using System;
using PixelProject.Core;
using PixelProject.Combat;
using PixelProject.Utilities;

namespace PixelProject.Enemies
{
    /// <summary>
    /// Base class for all enemies with health, movement, and combat behavior.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        [Header("Identity")]
        [SerializeField] protected EnemyData enemyData;
        [SerializeField] protected string enemyId;

        [Header("Combat")]
        [SerializeField] protected float currentHealth;
        [SerializeField] protected float attackCooldown;

        [Header("References")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;

        protected Rigidbody2D rb;
        protected Transform target;
        protected float attackTimer;
        protected bool isAlive = true;
        protected float difficultyMultiplier = 1f;

        // IDamageable implementation
        public float CurrentHealth => currentHealth;
        public float MaxHealth => enemyData != null ? enemyData.maxHealth * difficultyMultiplier : 100f;
        public bool IsAlive => isAlive && currentHealth > 0;

        public EnemyData Data => enemyData;

        public event Action<EnemyBase> OnDeath;
        public event Action<float, float> OnHealthChanged;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        protected virtual void Start()
        {
            Initialize();
        }

        public virtual void Initialize()
        {
            // Apply difficulty scaling
            difficultyMultiplier = GameManager.Instance?.GetDifficultyMultiplier() ?? 1f;

            currentHealth = MaxHealth;
            isAlive = true;
            attackTimer = 0f;

            // Find player target
            var player = Player.PlayerController.Instance;
            if (player != null)
            {
                target = player.transform;
            }
        }

        protected virtual void Update()
        {
            if (!isAlive) return;

            UpdateAttackCooldown();
            UpdateBehavior();
        }

        protected virtual void FixedUpdate()
        {
            if (!isAlive) return;

            UpdateMovement();
        }

        protected virtual void UpdateBehavior()
        {
            // Override in subclasses for specific AI behavior
        }

        protected virtual void UpdateMovement()
        {
            if (target == null) return;

            Vector2 direction = (target.position - transform.position).normalized;
            float speed = enemyData != null ? enemyData.moveSpeed : 3f;

            rb.linearVelocity = direction * speed;

            // Flip sprite based on movement direction
            if (spriteRenderer != null && direction.x != 0)
            {
                spriteRenderer.flipX = direction.x < 0;
            }
        }

        protected virtual void UpdateAttackCooldown()
        {
            if (attackTimer > 0)
            {
                attackTimer -= Time.deltaTime;
            }
        }

        protected virtual bool CanAttack()
        {
            if (target == null || attackTimer > 0) return false;

            float attackRange = enemyData != null ? enemyData.attackRange : 1.5f;
            float distance = Vector2.Distance(transform.position, target.position);

            return distance <= attackRange;
        }

        protected virtual void Attack()
        {
            if (!CanAttack()) return;

            attackTimer = attackCooldown;

            float damage = enemyData != null ? enemyData.damage * difficultyMultiplier : 10f;

            var playerStats = target.GetComponent<Player.PlayerStats>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(damage, transform.position);
            }

            animator?.SetTrigger("Attack");
        }

        public virtual void TakeDamage(DamageInfo damageInfo)
        {
            if (!isAlive) return;

            float actualDamage = CalculateDamage(damageInfo);
            currentHealth -= actualDamage;

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            // Visual feedback
            StartCoroutine(DamageFlash());

            // Knockback
            if (rb != null && damageInfo.Direction != Vector2.zero)
            {
                float knockbackForce = enemyData != null ? enemyData.knockbackResistance : 1f;
                rb.AddForce(damageInfo.Direction * (5f / knockbackForce), ForceMode2D.Impulse);
            }

            // Spawn damage number
            SpawnDamageNumber(actualDamage, damageInfo.IsCritical);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        protected virtual float CalculateDamage(DamageInfo damageInfo)
        {
            float damage = damageInfo.Damage;

            // Apply armor if physical damage
            if (damageInfo.DamageType == DamageType.Physical && enemyData != null)
            {
                damage = DamageCalculator.CalculatePhysicalDamage(damage, enemyData.armor);
            }

            return damage;
        }

        protected virtual System.Collections.IEnumerator DamageFlash()
        {
            if (spriteRenderer == null) yield break;

            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
        }

        protected virtual void SpawnDamageNumber(float damage, bool isCritical)
        {
            // Would instantiate a damage number popup here
            Debug.Log($"Enemy took {damage:F0} damage{(isCritical ? " (CRIT!)" : "")}");
        }

        public virtual void Heal(float amount)
        {
            if (!isAlive) return;

            currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        protected virtual void Die()
        {
            if (!isAlive) return;

            isAlive = false;
            rb.linearVelocity = Vector2.zero;

            // Drop loot
            DropLoot();

            // Notify systems
            GameManager.Instance?.RegisterEnemyKill();

            int goldDrop = enemyData != null ? enemyData.goldDrop : 5;
            int expDrop = enemyData != null ? enemyData.experienceDrop : 10;

            EventBus.Publish(new EnemyKilledEvent
            {
                Enemy = gameObject,
                EnemyType = enemyId,
                Position = transform.position,
                GoldDropped = goldDrop,
                ExperienceGained = expDrop
            });

            OnDeath?.Invoke(this);

            // Play death animation or destroy
            animator?.SetTrigger("Death");
            Destroy(gameObject, 0.5f);
        }

        protected virtual void DropLoot()
        {
            if (enemyData == null) return;

            // Drop gold
            if (enemyData.goldDrop > 0)
            {
                // Would spawn gold pickup here
            }

            // Drop experience
            if (enemyData.experienceDrop > 0)
            {
                // Would spawn exp orb here
            }

            // Chance to drop item
            if (UnityEngine.Random.value < enemyData.itemDropChance)
            {
                // Would spawn item drop here
            }
        }

        protected virtual void OnCollisionStay2D(Collision2D collision)
        {
            // Contact damage
            if (!isAlive) return;

            var playerStats = collision.gameObject.GetComponent<Player.PlayerStats>();
            var playerController = collision.gameObject.GetComponent<Player.PlayerController>();

            if (playerStats != null && playerController != null)
            {
                // Don't damage if player is invincible (dashing)
                if (playerController.IsInvincible) return;

                float contactDamage = enemyData != null ? enemyData.contactDamage : 5f;
                playerStats.TakeDamage(contactDamage * Time.deltaTime * 10f, transform.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (enemyData == null) return;

            // Draw attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);

            // Draw detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, enemyData.detectionRange);
        }
    }
}
