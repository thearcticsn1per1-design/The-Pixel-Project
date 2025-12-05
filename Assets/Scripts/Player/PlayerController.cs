using UnityEngine;
using PixelProject.Core;
using PixelProject.Combat;

namespace PixelProject.Player
{
    /// <summary>
    /// Main player controller handling movement, aiming, and input.
    /// Designed for top-down or side-scrolling roguelite gameplay.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float acceleration = 50f;
        [SerializeField] private float deceleration = 50f;
        [SerializeField] private bool smoothMovement = true;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 15f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 1f;
        [SerializeField] private bool dashInvincible = true;

        [Header("References")]
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Rigidbody2D rb;
        private PlayerStats stats;
        private WeaponController weaponController;

        private Vector2 moveInput;
        private Vector2 currentVelocity;
        private Vector2 aimDirection;
        private bool isDashing;
        private float dashTimer;
        private float dashCooldownTimer;
        private Vector2 dashDirection;

        public Vector2 AimDirection => aimDirection;
        public bool IsDashing => isDashing;
        public bool IsInvincible => isDashing && dashInvincible;
        public float CurrentMoveSpeed => baseMoveSpeed * (stats?.MoveSpeedMultiplier ?? 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            rb = GetComponent<Rigidbody2D>();
            stats = GetComponent<PlayerStats>();
            weaponController = GetComponentInChildren<WeaponController>();

            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void Update()
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            HandleInput();
            HandleAiming();
            UpdateTimers();
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            if (isDashing)
            {
                HandleDash();
            }
            else
            {
                HandleMovement();
            }
        }

        private void HandleInput()
        {
            // Movement input (WASD or Arrow Keys)
            moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            ).normalized;

            // Dash input
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                TryDash();
            }

            // Fire input
            if (Input.GetMouseButton(0))
            {
                weaponController?.Fire();
            }

            // Secondary fire / ability
            if (Input.GetMouseButton(1))
            {
                weaponController?.SecondaryFire();
            }

            // Reload
            if (Input.GetKeyDown(KeyCode.R))
            {
                weaponController?.Reload();
            }
        }

        private void HandleAiming()
        {
            // Get mouse position in world space
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            // Calculate aim direction
            aimDirection = (mouseWorldPos - transform.position).normalized;

            // Rotate weapon pivot towards mouse
            if (weaponPivot != null)
            {
                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            // Flip sprite based on aim direction
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = aimDirection.x < 0;
            }
        }

        private void HandleMovement()
        {
            float targetSpeed = CurrentMoveSpeed;
            Vector2 targetVelocity = moveInput * targetSpeed;

            if (smoothMovement)
            {
                float accel = moveInput.magnitude > 0.01f ? acceleration : deceleration;
                currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accel * Time.fixedDeltaTime);
            }
            else
            {
                currentVelocity = targetVelocity;
            }

            rb.linearVelocity = currentVelocity;
        }

        private void TryDash()
        {
            if (isDashing || dashCooldownTimer > 0f) return;
            if (stats != null && !stats.CanDash) return;

            // Dash in move direction, or aim direction if not moving
            dashDirection = moveInput.magnitude > 0.1f ? moveInput.normalized : aimDirection;
            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown * (stats?.DashCooldownMultiplier ?? 1f);

            // Visual feedback
            if (spriteRenderer != null)
            {
                // Could add dash trail effect here
            }
        }

        private void HandleDash()
        {
            float currentDashSpeed = dashSpeed * (stats?.DashSpeedMultiplier ?? 1f);
            rb.linearVelocity = dashDirection * currentDashSpeed;

            dashTimer -= Time.fixedDeltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                currentVelocity = rb.linearVelocity * 0.5f; // Momentum preservation
            }
        }

        private void UpdateTimers()
        {
            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= Time.deltaTime;
            }
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (isDashing) return;
            rb.AddForce(direction.normalized * force, ForceMode2D.Impulse);
        }

        public void Teleport(Vector3 position)
        {
            transform.position = position;
            rb.linearVelocity = Vector2.zero;
            currentVelocity = Vector2.zero;
        }

        public void SetMoveSpeed(float speed)
        {
            baseMoveSpeed = speed;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
