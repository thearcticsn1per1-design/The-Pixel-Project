using UnityEngine;
using System;
using System.Collections.Generic;
using PixelProject.Core;
using PixelProject.Player;

namespace PixelProject.Items
{
    /// <summary>
    /// Manages player inventory, items, and their effects.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int maxPassiveItems = 12;
        [SerializeField] private int maxActiveItems = 2;

        private List<InventorySlot> passiveItems = new List<InventorySlot>();
        private List<InventorySlot> activeItems = new List<InventorySlot>();
        private Dictionary<string, int> itemCounts = new Dictionary<string, int>();

        private PlayerStats playerStats;

        public List<InventorySlot> PassiveItems => passiveItems;
        public List<InventorySlot> ActiveItems => activeItems;

        public event Action<ItemData> OnItemAdded;
        public event Action<ItemData> OnItemRemoved;
        public event Action OnInventoryChanged;

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
            playerStats = PlayerController.Instance?.GetComponent<PlayerStats>();

            // Subscribe to events for item effects
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void Update()
        {
            // Update item effects
            foreach (var slot in passiveItems)
            {
                slot.ItemData?.specialEffect?.OnUpdate(playerStats);
            }

            // Check for active item usage
            HandleActiveItemInput();
        }

        private void HandleActiveItemInput()
        {
            // Q and E for active items
            if (Input.GetKeyDown(KeyCode.Q) && activeItems.Count > 0)
            {
                UseActiveItem(0);
            }
            if (Input.GetKeyDown(KeyCode.E) && activeItems.Count > 1)
            {
                UseActiveItem(1);
            }
        }

        public bool AddItem(ItemData item, int quantity = 1)
        {
            if (item == null) return false;

            // Check if item already exists and is stackable
            if (item.isStackable)
            {
                InventorySlot existingSlot = FindSlotWithItem(item.itemId);
                if (existingSlot != null)
                {
                    int newQuantity = Mathf.Min(existingSlot.Quantity + quantity, item.maxStack);
                    int added = newQuantity - existingSlot.Quantity;
                    existingSlot.Quantity = newQuantity;

                    UpdateItemCount(item.itemId, added);
                    ApplyItemEffects(item, added);

                    OnItemAdded?.Invoke(item);
                    OnInventoryChanged?.Invoke();

                    return true;
                }
            }

            // Add new slot
            List<InventorySlot> targetList = item.itemType == ItemType.Active ? activeItems : passiveItems;
            int maxSlots = item.itemType == ItemType.Active ? maxActiveItems : maxPassiveItems;

            if (targetList.Count >= maxSlots)
            {
                Debug.Log($"Inventory full! Cannot add {item.itemName}");
                return false;
            }

            InventorySlot newSlot = new InventorySlot
            {
                ItemData = item,
                Quantity = Mathf.Min(quantity, item.maxStack)
            };

            targetList.Add(newSlot);
            UpdateItemCount(item.itemId, newSlot.Quantity);
            ApplyItemEffects(item, newSlot.Quantity);

            OnItemAdded?.Invoke(item);
            OnInventoryChanged?.Invoke();

            Debug.Log($"Added {item.itemName} x{quantity} to inventory");
            return true;
        }

        public bool RemoveItem(string itemId, int quantity = 1)
        {
            InventorySlot slot = FindSlotWithItem(itemId);
            if (slot == null) return false;

            int removed = Mathf.Min(quantity, slot.Quantity);
            slot.Quantity -= removed;

            UpdateItemCount(itemId, -removed);
            RemoveItemEffects(slot.ItemData, removed);

            if (slot.Quantity <= 0)
            {
                passiveItems.Remove(slot);
                activeItems.Remove(slot);
            }

            OnItemRemoved?.Invoke(slot.ItemData);
            OnInventoryChanged?.Invoke();

            return true;
        }

        public bool HasItem(string itemId)
        {
            return GetItemCount(itemId) > 0;
        }

        public int GetItemCount(string itemId)
        {
            return itemCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        private InventorySlot FindSlotWithItem(string itemId)
        {
            foreach (var slot in passiveItems)
            {
                if (slot.ItemData.itemId == itemId) return slot;
            }
            foreach (var slot in activeItems)
            {
                if (slot.ItemData.itemId == itemId) return slot;
            }
            return null;
        }

        private void UpdateItemCount(string itemId, int change)
        {
            if (!itemCounts.ContainsKey(itemId))
            {
                itemCounts[itemId] = 0;
            }
            itemCounts[itemId] += change;

            if (itemCounts[itemId] <= 0)
            {
                itemCounts.Remove(itemId);
            }
        }

        private void ApplyItemEffects(ItemData item, int quantity)
        {
            if (playerStats == null) return;

            // Apply stat modifiers
            foreach (var mod in item.statModifiers)
            {
                for (int i = 0; i < quantity; i++)
                {
                    playerStats.AddModifier(mod.statType, new StatModifier(
                        mod.value,
                        mod.modifierType,
                        $"item_{item.itemId}"
                    ));
                }
            }

            // Apply special effect
            item.specialEffect?.OnPickup(playerStats);
        }

        private void RemoveItemEffects(ItemData item, int quantity)
        {
            if (playerStats == null) return;

            // Remove stat modifiers
            for (int i = 0; i < quantity; i++)
            {
                foreach (var mod in item.statModifiers)
                {
                    playerStats.RemoveModifier(mod.statType, $"item_{item.itemId}");
                }
            }

            // Check if item is completely removed
            if (GetItemCount(item.itemId) <= 0)
            {
                item.specialEffect?.OnRemove(playerStats);
            }
        }

        private void UseActiveItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= activeItems.Count) return;

            var slot = activeItems[slotIndex];
            if (slot.IsOnCooldown) return;

            // Use the active item
            // Implementation depends on the specific active item

            if (slot.ItemData.itemType == ItemType.Consumable)
            {
                // Consumable items are removed after use
                RemoveItem(slot.ItemData.itemId, 1);
            }
            else
            {
                // Set cooldown for reusable active items
                slot.CooldownTimer = 10f; // Default cooldown
            }

            Debug.Log($"Used active item: {slot.ItemData.itemName}");
        }

        // Event handlers for item effects
        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            foreach (var slot in passiveItems)
            {
                slot.ItemData?.specialEffect?.OnEnemyKilled(playerStats);
            }
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            foreach (var slot in passiveItems)
            {
                slot.ItemData?.specialEffect?.OnPlayerDamaged(playerStats, evt.Damage);
            }
        }

        public void ClearInventory()
        {
            // Remove all item effects
            foreach (var slot in passiveItems)
            {
                RemoveItemEffects(slot.ItemData, slot.Quantity);
            }
            foreach (var slot in activeItems)
            {
                RemoveItemEffects(slot.ItemData, slot.Quantity);
            }

            passiveItems.Clear();
            activeItems.Clear();
            itemCounts.Clear();

            OnInventoryChanged?.Invoke();
        }
    }

    [Serializable]
    public class InventorySlot
    {
        public ItemData ItemData;
        public int Quantity;
        public float CooldownTimer;

        public bool IsOnCooldown => CooldownTimer > 0;
    }
}
