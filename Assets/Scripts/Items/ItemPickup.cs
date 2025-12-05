using UnityEngine;
using PixelProject.Core;
using PixelProject.Player;

namespace PixelProject.Items
{
    /// <summary>
    /// Component for items that can be picked up in the world.
    /// </summary>
    public class ItemPickup : MonoBehaviour
    {
        [Header("Item")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int quantity = 1;

        [Header("Pickup Settings")]
        [SerializeField] private float pickupRadius = 1f;
        [SerializeField] private bool autoPickup = true;
        [SerializeField] private float magnetSpeed = 10f;
        [SerializeField] private float magnetRange = 3f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobAmount = 0.2f;
        [SerializeField] private bool rotates = false;
        [SerializeField] private float rotationSpeed = 90f;

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSound;

        private Vector3 startPosition;
        private float bobOffset;
        private Transform playerTransform;
        private bool isBeingMagneted;

        public ItemData Data => itemData;
        public int Quantity => quantity;

        private void Start()
        {
            startPosition = transform.position;
            bobOffset = Random.Range(0f, Mathf.PI * 2f);

            if (spriteRenderer != null && itemData != null)
            {
                if (itemData.icon != null)
                {
                    spriteRenderer.sprite = itemData.icon;
                }
                spriteRenderer.color = itemData.rarityColor;
            }

            var player = PlayerController.Instance;
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            // Visual effects
            UpdateVisuals();

            // Check for magnet pickup
            if (autoPickup && playerTransform != null)
            {
                CheckMagnetPickup();
            }
        }

        private void UpdateVisuals()
        {
            // Bobbing animation
            float bob = Mathf.Sin((Time.time + bobOffset) * bobSpeed) * bobAmount;

            if (!isBeingMagneted)
            {
                transform.position = startPosition + Vector3.up * bob;
            }

            // Rotation
            if (rotates)
            {
                transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
            }
        }

        private void CheckMagnetPickup()
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // Get player's pickup range
            float pickupRange = magnetRange;
            var playerStats = playerTransform.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                pickupRange = playerStats.PickupRange;
            }

            if (distance <= pickupRange)
            {
                isBeingMagneted = true;

                // Move towards player
                Vector3 direction = (playerTransform.position - transform.position).normalized;
                float speed = magnetSpeed * (1f + (pickupRange - distance) / pickupRange);
                transform.position += direction * speed * Time.deltaTime;

                // Check if close enough to pickup
                if (distance <= pickupRadius)
                {
                    Pickup();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!autoPickup && other.CompareTag("Player"))
            {
                Pickup();
            }
        }

        public void Pickup()
        {
            var inventory = InventoryManager.Instance;
            var playerStats = PlayerController.Instance?.GetComponent<PlayerStats>();

            if (itemData == null) return;

            bool success = false;

            switch (itemData.itemType)
            {
                case ItemType.Currency:
                    if (playerStats != null)
                    {
                        if (itemData.itemId == "gold")
                        {
                            playerStats.AddGold(quantity);
                            success = true;
                        }
                        else if (itemData.itemId == "experience")
                        {
                            playerStats.AddExperience(quantity);
                            success = true;
                        }
                    }
                    break;

                case ItemType.Consumable:
                    if (inventory != null)
                    {
                        success = inventory.AddItem(itemData, quantity);
                    }
                    break;

                default:
                    if (inventory != null)
                    {
                        success = inventory.AddItem(itemData, quantity);
                    }
                    break;
            }

            if (success)
            {
                EventBus.Publish(new ItemCollectedEvent
                {
                    ItemId = itemData.itemId,
                    ItemName = itemData.itemName,
                    Rarity = itemData.rarity
                });

                // Play pickup sound
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }

                // Destroy pickup
                Destroy(gameObject);
            }
        }

        public void Initialize(ItemData data, int qty = 1)
        {
            itemData = data;
            quantity = qty;

            if (spriteRenderer != null && data != null && data.icon != null)
            {
                spriteRenderer.sprite = data.icon;
                spriteRenderer.color = data.rarityColor;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, magnetRange);
        }
    }

    /// <summary>
    /// Specialized pickup for experience orbs.
    /// </summary>
    public class ExperienceOrb : MonoBehaviour
    {
        [SerializeField] private int experienceValue = 10;
        [SerializeField] private float magnetSpeed = 15f;
        [SerializeField] private float pickupRadius = 0.5f;

        private Transform playerTransform;
        private bool isBeingMagneted;

        private void Start()
        {
            var player = PlayerController.Instance;
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            float distance = Vector2.Distance(transform.position, playerTransform.position);

            var playerStats = playerTransform.GetComponent<PlayerStats>();
            float pickupRange = playerStats?.PickupRange ?? 3f;

            if (distance <= pickupRange)
            {
                isBeingMagneted = true;

                Vector3 direction = (playerTransform.position - transform.position).normalized;
                transform.position += direction * magnetSpeed * Time.deltaTime;

                if (distance <= pickupRadius)
                {
                    Collect();
                }
            }
        }

        private void Collect()
        {
            var playerStats = playerTransform?.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.AddExperience(experienceValue);
            }

            Destroy(gameObject);
        }

        public void SetValue(int value)
        {
            experienceValue = value;
            // Could scale size based on value
            float scale = 0.5f + (value / 50f) * 0.5f;
            transform.localScale = Vector3.one * Mathf.Min(scale, 1.5f);
        }
    }
}
