using UnityEngine;

namespace PixelProject.Player
{
    /// <summary>
    /// Handles player sprite animations including movement, attacks, and special effects.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerStats stats;

        [Header("Animation Parameters")]
        [SerializeField] private string moveSpeedParam = "MoveSpeed";
        [SerializeField] private string isDashingParam = "IsDashing";
        [SerializeField] private string isAttackingParam = "IsAttacking";
        [SerializeField] private string hurtTrigger = "Hurt";
        [SerializeField] private string deathTrigger = "Death";

        [Header("Visual Effects")]
        [SerializeField] private float damageFlashDuration = 0.1f;
        [SerializeField] private Color damageFlashColor = Color.red;
        [SerializeField] private TrailRenderer dashTrail;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private float flashTimer;
        private bool isFlashing;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }

            if (controller == null)
            {
                controller = GetComponentInParent<PlayerController>();
            }

            if (stats == null)
            {
                stats = GetComponentInParent<PlayerStats>();
            }
        }

        private void OnEnable()
        {
            if (stats != null)
            {
                stats.OnHealthChanged += OnHealthChanged;
                stats.OnDeath += OnDeath;
            }
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.OnHealthChanged -= OnHealthChanged;
                stats.OnDeath -= OnDeath;
            }
        }

        private void Update()
        {
            UpdateAnimationParameters();
            UpdateDamageFlash();
            UpdateDashTrail();
        }

        private void UpdateAnimationParameters()
        {
            if (animator == null || controller == null) return;

            // Movement speed for walk/run animations
            float moveSpeed = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).magnitude;
            animator.SetFloat(moveSpeedParam, moveSpeed);

            // Dash state
            animator.SetBool(isDashingParam, controller.IsDashing);
        }

        private void OnHealthChanged(float current, float max)
        {
            // Check if damage was taken
            if (current < max)
            {
                TriggerDamageFlash();
                animator?.SetTrigger(hurtTrigger);
            }
        }

        private void OnDeath()
        {
            animator?.SetTrigger(deathTrigger);
            StopAllCoroutines();
        }

        private void TriggerDamageFlash()
        {
            isFlashing = true;
            flashTimer = damageFlashDuration;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = damageFlashColor;
            }
        }

        private void UpdateDamageFlash()
        {
            if (!isFlashing) return;

            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0)
            {
                isFlashing = false;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = originalColor;
                }
            }
        }

        private void UpdateDashTrail()
        {
            if (dashTrail == null || controller == null) return;

            dashTrail.emitting = controller.IsDashing;
        }

        public void PlayAttackAnimation()
        {
            animator?.SetTrigger(isAttackingParam);
        }

        public void SetColor(Color color)
        {
            if (spriteRenderer != null)
            {
                originalColor = color;
                if (!isFlashing)
                {
                    spriteRenderer.color = color;
                }
            }
        }

        public void ResetColor()
        {
            originalColor = Color.white;
            if (spriteRenderer != null && !isFlashing)
            {
                spriteRenderer.color = originalColor;
            }
        }
    }
}
