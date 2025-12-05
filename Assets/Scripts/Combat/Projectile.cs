using UnityEngine;
using PixelProject.Utilities;

namespace PixelProject.Combat
{
    /// <summary>
    /// Base projectile class handling movement, collision, and damage.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private LayerMask hitLayers;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private bool destroyOnHit = true;

        protected Rigidbody2D rb;
        protected float damage;
        protected float speed;
        protected bool isCritical;
        protected bool piercing;
        protected int pierceCount;
        protected int currentPierces;
        protected Vector2 direction;

        private float lifeTimer;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public virtual void Initialize(Vector2 dir, float spd, float dmg, bool crit = false, bool pierce = false, int pierceAmount = 1)
        {
            direction = dir.normalized;
            speed = spd;
            damage = dmg;
            isCritical = crit;
            piercing = pierce;
            pierceCount = pierceAmount;
            currentPierces = 0;
            lifeTimer = lifetime;

            rb.linearVelocity = direction * speed;

            gameObject.SetActive(true);
        }

        protected virtual void Update()
        {
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0)
            {
                Deactivate();
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsValidTarget(other)) return;

            // Deal damage
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                DamageInfo damageInfo = new DamageInfo
                {
                    Damage = damage,
                    IsCritical = isCritical,
                    Source = transform.position,
                    Direction = direction,
                    DamageType = DamageType.Physical
                };

                damageable.TakeDamage(damageInfo);
            }

            // Spawn hit effect
            SpawnHitEffect(other.ClosestPoint(transform.position));

            // Handle piercing
            if (piercing && currentPierces < pierceCount)
            {
                currentPierces++;
                damage *= 0.8f; // Reduce damage per pierce
            }
            else if (destroyOnHit)
            {
                Deactivate();
            }
        }

        protected virtual bool IsValidTarget(Collider2D other)
        {
            return ((1 << other.gameObject.layer) & hitLayers) != 0;
        }

        protected virtual void SpawnHitEffect(Vector2 position)
        {
            if (hitEffectPrefab == null) return;

            GameObject effect = ObjectPool.Instance?.Get(hitEffectPrefab);
            if (effect == null)
            {
                effect = Instantiate(hitEffectPrefab);
            }

            effect.transform.position = position;
            effect.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            // Auto-disable after effect duration
            var particles = effect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                float duration = particles.main.duration + particles.main.startLifetime.constantMax;
                ObjectPool.Instance?.ReturnAfterDelay(effect, hitEffectPrefab, duration);
            }
        }

        protected virtual void Deactivate()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Return(gameObject, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnBecameInvisible()
        {
            Deactivate();
        }
    }

    /// <summary>
    /// Explosive projectile that deals area damage on impact.
    /// </summary>
    public class ExplosiveProjectile : Projectile
    {
        [Header("Explosion")]
        [SerializeField] private float explosionRadius = 2f;
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField] private LayerMask explosionLayers;
        [SerializeField] private float explosionForce = 5f;

        private float explosionDamage;

        public void Initialize(float dmg, float radius)
        {
            explosionDamage = dmg;
            explosionRadius = radius;
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsValidTarget(other)) return;

            Explode();
        }

        private void Explode()
        {
            // Spawn explosion effect
            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            }

            // Find all targets in radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, explosionLayers);

            foreach (var hit in hits)
            {
                // Calculate damage falloff based on distance
                float distance = Vector2.Distance(transform.position, hit.transform.position);
                float falloff = 1f - (distance / explosionRadius);
                float actualDamage = explosionDamage * falloff;

                // Deal damage
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Vector2 direction = (hit.transform.position - transform.position).normalized;
                    DamageInfo damageInfo = new DamageInfo
                    {
                        Damage = actualDamage,
                        IsCritical = false,
                        Source = transform.position,
                        Direction = direction,
                        DamageType = DamageType.Explosive
                    };

                    damageable.TakeDamage(damageInfo);
                }

                // Apply knockback
                var rb = hit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
                    rb.AddForce(knockbackDir * explosionForce * falloff, ForceMode2D.Impulse);
                }
            }

            Deactivate();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }

    /// <summary>
    /// Homing projectile that tracks targets.
    /// </summary>
    public class HomingProjectile : Projectile
    {
        [Header("Homing")]
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float detectionRadius = 10f;
        [SerializeField] private LayerMask targetLayers;

        private Transform target;

        protected override void Update()
        {
            base.Update();

            if (target == null)
            {
                FindTarget();
            }

            if (target != null)
            {
                TrackTarget();
            }
        }

        private void FindTarget()
        {
            Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayers);

            float closestDistance = float.MaxValue;
            foreach (var col in potentialTargets)
            {
                float distance = Vector2.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    target = col.transform;
                }
            }
        }

        private void TrackTarget()
        {
            Vector2 targetDirection = (target.position - transform.position).normalized;
            float angle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            float newAngle = Mathf.MoveTowardsAngle(currentAngle, angle, turnSpeed * Time.deltaTime);
            direction = new Vector2(Mathf.Cos(newAngle * Mathf.Deg2Rad), Mathf.Sin(newAngle * Mathf.Deg2Rad));

            rb.linearVelocity = direction * speed;
            transform.rotation = Quaternion.Euler(0, 0, newAngle);
        }
    }
}
