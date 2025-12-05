using UnityEngine;
using PixelProject.Player;

namespace PixelProject.Utilities
{
    /// <summary>
    /// Camera controller with smooth following, screen shake, and boundary constraints.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f);
        [SerializeField] private bool lookAhead = true;
        [SerializeField] private float lookAheadDistance = 2f;
        [SerializeField] private float lookAheadSpeed = 3f;

        [Header("Bounds")]
        [SerializeField] private bool useBounds = false;
        [SerializeField] private Vector2 minBounds;
        [SerializeField] private Vector2 maxBounds;

        [Header("Screen Shake")]
        [SerializeField] private float maxShakeDuration = 1f;
        [SerializeField] private AnimationCurve shakeFalloff = AnimationCurve.Linear(0, 1, 1, 0);

        private Vector3 currentVelocity;
        private Vector2 lookAheadOffset;
        private float shakeTimer;
        private float shakeMagnitude;
        private Vector3 shakeOffset;

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
            // Find player if not set
            if (target == null)
            {
                var player = PlayerController.Instance;
                if (player != null)
                {
                    target = player.transform;
                }
            }

            // Set initial position
            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Calculate target position
            Vector3 targetPosition = target.position + offset;

            // Look ahead based on player movement/aim
            if (lookAhead)
            {
                UpdateLookAhead();
                targetPosition += (Vector3)lookAheadOffset;
            }

            // Smooth follow
            Vector3 newPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, 1f / smoothSpeed);

            // Apply bounds
            if (useBounds)
            {
                newPosition = ApplyBounds(newPosition);
            }

            // Apply screen shake
            UpdateShake();
            newPosition += shakeOffset;

            transform.position = newPosition;
        }

        private void UpdateLookAhead()
        {
            Vector2 targetLookAhead = Vector2.zero;

            var player = target.GetComponent<PlayerController>();
            if (player != null)
            {
                targetLookAhead = player.AimDirection * lookAheadDistance;
            }

            lookAheadOffset = Vector2.Lerp(lookAheadOffset, targetLookAhead, lookAheadSpeed * Time.deltaTime);
        }

        private Vector3 ApplyBounds(Vector3 position)
        {
            Camera cam = Camera.main;
            if (cam == null) return position;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            position.x = Mathf.Clamp(position.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
            position.y = Mathf.Clamp(position.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);

            return position;
        }

        private void UpdateShake()
        {
            if (shakeTimer <= 0)
            {
                shakeOffset = Vector3.zero;
                return;
            }

            shakeTimer -= Time.deltaTime;

            float progress = 1f - (shakeTimer / maxShakeDuration);
            float currentMagnitude = shakeMagnitude * shakeFalloff.Evaluate(progress);

            shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * currentMagnitude,
                Random.Range(-1f, 1f) * currentMagnitude,
                0f
            );
        }

        /// <summary>
        /// Triggers screen shake effect.
        /// </summary>
        public void Shake(float magnitude, float duration)
        {
            if (magnitude > shakeMagnitude || shakeTimer <= 0)
            {
                shakeMagnitude = magnitude;
                shakeTimer = Mathf.Min(duration, maxShakeDuration);
            }
        }

        /// <summary>
        /// Sets a new target for the camera to follow.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Sets the camera bounds.
        /// </summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;
            useBounds = true;
        }

        /// <summary>
        /// Disables camera bounds.
        /// </summary>
        public void ClearBounds()
        {
            useBounds = false;
        }

        /// <summary>
        /// Instantly moves camera to target position.
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            transform.position = target.position + offset;
            currentVelocity = Vector3.zero;
            lookAheadOffset = Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            if (!useBounds) return;

            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3((minBounds.x + maxBounds.x) / 2f, (minBounds.y + maxBounds.y) / 2f, 0);
            Vector3 size = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
