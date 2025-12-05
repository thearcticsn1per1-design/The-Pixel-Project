using UnityEngine;
using PixelProject.Combat;
using PixelProject.Utilities;

namespace PixelProject.Enemies
{
    /// <summary>
    /// Ranged enemy that keeps distance and fires projectiles.
    /// </summary>
    public class RangedEnemy : EnemyBase
    {
        [Header("Ranged Settings")]
        [SerializeField] private float preferredDistance = 5f;
        [SerializeField] private float projectileSpeed = 8f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;

        protected override void UpdateMovement()
        {
            if (target == null) return;

            float distance = Vector2.Distance(transform.position, target.position);
            Vector2 direction;

            if (distance < preferredDistance - 1f)
            {
                // Too close, back away
                direction = (transform.position - target.position).normalized;
            }
            else if (distance > preferredDistance + 1f)
            {
                // Too far, move closer
                direction = (target.position - transform.position).normalized;
            }
            else
            {
                // Good distance, strafe
                direction = Vector2.Perpendicular((target.position - transform.position).normalized);
                if (Random.value > 0.5f) direction = -direction;
            }

            float speed = enemyData != null ? enemyData.moveSpeed : 3f;
            rb.linearVelocity = direction * speed;

            // Always face the player
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = target.position.x < transform.position.x;
            }
        }

        protected override void UpdateBehavior()
        {
            if (CanAttack())
            {
                Attack();
            }
        }

        protected override void Attack()
        {
            if (target == null) return;

            attackTimer = attackCooldown;

            // Fire projectile
            if (projectilePrefab != null && firePoint != null)
            {
                Vector2 direction = (target.position - firePoint.position).normalized;
                float damage = enemyData != null ? enemyData.damage * difficultyMultiplier : 10f;

                GameObject proj = ObjectPool.Instance?.Get(projectilePrefab);
                if (proj == null)
                {
                    proj = Instantiate(projectilePrefab);
                }

                proj.transform.position = firePoint.position;
                proj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

                var projectile = proj.GetComponent<EnemyProjectile>();
                if (projectile != null)
                {
                    projectile.Initialize(direction, projectileSpeed, damage);
                }
            }

            animator?.SetTrigger("Attack");
        }
    }

    /// <summary>
    /// Charger enemy that builds up speed and charges at player.
    /// </summary>
    public class ChargerEnemy : EnemyBase
    {
        [Header("Charge Settings")]
        [SerializeField] private float chargeSpeed = 12f;
        [SerializeField] private float chargeWindup = 0.5f;
        [SerializeField] private float chargeDuration = 1f;
        [SerializeField] private float chargeRecovery = 1f;
        [SerializeField] private float chargeDamageMultiplier = 2f;

        private enum ChargeState { Idle, Windup, Charging, Recovery }
        private ChargeState chargeState = ChargeState.Idle;
        private float stateTimer;
        private Vector2 chargeDirection;

        protected override void UpdateBehavior()
        {
            stateTimer -= Time.deltaTime;

            switch (chargeState)
            {
                case ChargeState.Idle:
                    if (CanAttack())
                    {
                        StartWindup();
                    }
                    break;

                case ChargeState.Windup:
                    if (stateTimer <= 0)
                    {
                        StartCharge();
                    }
                    break;

                case ChargeState.Charging:
                    if (stateTimer <= 0)
                    {
                        EndCharge();
                    }
                    break;

                case ChargeState.Recovery:
                    if (stateTimer <= 0)
                    {
                        chargeState = ChargeState.Idle;
                    }
                    break;
            }
        }

        protected override void UpdateMovement()
        {
            if (target == null) return;

            switch (chargeState)
            {
                case ChargeState.Idle:
                    base.UpdateMovement();
                    break;

                case ChargeState.Windup:
                    rb.linearVelocity = Vector2.zero;
                    // Could add visual shake/telegraph here
                    break;

                case ChargeState.Charging:
                    rb.linearVelocity = chargeDirection * chargeSpeed;
                    break;

                case ChargeState.Recovery:
                    rb.linearVelocity *= 0.9f; // Slow down
                    break;
            }
        }

        private void StartWindup()
        {
            chargeState = ChargeState.Windup;
            stateTimer = chargeWindup;
            chargeDirection = (target.position - transform.position).normalized;

            // Visual feedback (e.g., change color)
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.yellow;
            }
        }

        private void StartCharge()
        {
            chargeState = ChargeState.Charging;
            stateTimer = chargeDuration;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.red;
            }
        }

        private void EndCharge()
        {
            chargeState = ChargeState.Recovery;
            stateTimer = chargeRecovery;
            attackTimer = attackCooldown;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }

        protected override void OnCollisionStay2D(Collision2D collision)
        {
            if (chargeState == ChargeState.Charging)
            {
                var playerStats = collision.gameObject.GetComponent<Player.PlayerStats>();
                var playerController = collision.gameObject.GetComponent<Player.PlayerController>();

                if (playerStats != null && playerController != null && !playerController.IsInvincible)
                {
                    float damage = (enemyData?.damage ?? 10f) * chargeDamageMultiplier * difficultyMultiplier;
                    playerStats.TakeDamage(damage, transform.position);

                    // End charge on impact
                    EndCharge();
                }
            }
            else
            {
                base.OnCollisionStay2D(collision);
            }
        }
    }

    /// <summary>
    /// Swarm enemy - weak individually but spawns in groups.
    /// </summary>
    public class SwarmEnemy : EnemyBase
    {
        [Header("Swarm Settings")]
        [SerializeField] private float separationRadius = 1f;
        [SerializeField] private float separationWeight = 1f;

        protected override void UpdateMovement()
        {
            if (target == null) return;

            // Base movement toward player
            Vector2 toPlayer = (target.position - transform.position).normalized;

            // Separation from other swarm enemies
            Vector2 separation = CalculateSeparation();

            // Combined movement
            Vector2 direction = (toPlayer + separation * separationWeight).normalized;
            float speed = enemyData != null ? enemyData.moveSpeed : 3f;

            rb.linearVelocity = direction * speed;

            if (spriteRenderer != null && direction.x != 0)
            {
                spriteRenderer.flipX = direction.x < 0;
            }
        }

        private Vector2 CalculateSeparation()
        {
            Vector2 separation = Vector2.zero;
            int count = 0;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, separationRadius);

            foreach (var col in nearby)
            {
                if (col.gameObject == gameObject) continue;

                var otherEnemy = col.GetComponent<SwarmEnemy>();
                if (otherEnemy != null)
                {
                    Vector2 away = transform.position - otherEnemy.transform.position;
                    separation += away.normalized / away.magnitude;
                    count++;
                }
            }

            if (count > 0)
            {
                separation /= count;
            }

            return separation;
        }
    }

    /// <summary>
    /// Enemy projectile class for ranged enemy attacks.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private LayerMask playerLayer;

        private Rigidbody2D rb;
        private float damage;
        private float lifeTimer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
            }
        }

        public void Initialize(Vector2 direction, float speed, float dmg)
        {
            damage = dmg;
            lifeTimer = lifetime;

            if (rb != null)
            {
                rb.linearVelocity = direction * speed;
            }

            gameObject.SetActive(true);
        }

        private void Update()
        {
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            var playerStats = other.GetComponent<Player.PlayerStats>();
            var playerController = other.GetComponent<Player.PlayerController>();

            if (playerStats != null)
            {
                if (playerController != null && playerController.IsInvincible) return;

                playerStats.TakeDamage(damage, transform.position);
            }

            Destroy(gameObject);
        }
    }
}
