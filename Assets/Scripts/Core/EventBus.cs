using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelProject.Core
{
    /// <summary>
    /// Generic event bus for decoupled communication between game systems.
    /// Supports parameterized events with type safety.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> eventHandlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            Type eventType = typeof(T);

            if (eventHandlers.TryGetValue(eventType, out Delegate existingHandler))
            {
                eventHandlers[eventType] = Delegate.Combine(existingHandler, handler);
            }
            else
            {
                eventHandlers[eventType] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            Type eventType = typeof(T);

            if (eventHandlers.TryGetValue(eventType, out Delegate existingHandler))
            {
                Delegate newHandler = Delegate.Remove(existingHandler, handler);
                if (newHandler == null)
                {
                    eventHandlers.Remove(eventType);
                }
                else
                {
                    eventHandlers[eventType] = newHandler;
                }
            }
        }

        public static void Publish<T>(T gameEvent) where T : struct, IGameEvent
        {
            Type eventType = typeof(T);

            if (eventHandlers.TryGetValue(eventType, out Delegate handler))
            {
                ((Action<T>)handler)?.Invoke(gameEvent);
            }
        }

        public static void Clear()
        {
            eventHandlers.Clear();
        }

        public static void ClearEvent<T>() where T : struct, IGameEvent
        {
            eventHandlers.Remove(typeof(T));
        }
    }

    /// <summary>
    /// Marker interface for game events
    /// </summary>
    public interface IGameEvent { }

    // Common Game Events

    public struct PlayerDamagedEvent : IGameEvent
    {
        public float Damage;
        public float CurrentHealth;
        public float MaxHealth;
        public Vector3 DamageSource;
    }

    public struct PlayerHealedEvent : IGameEvent
    {
        public float Amount;
        public float CurrentHealth;
        public float MaxHealth;
    }

    public struct PlayerDeathEvent : IGameEvent
    {
        public Vector3 DeathPosition;
    }

    public struct EnemySpawnedEvent : IGameEvent
    {
        public GameObject Enemy;
        public string EnemyType;
        public Vector3 Position;
    }

    public struct EnemyKilledEvent : IGameEvent
    {
        public GameObject Enemy;
        public string EnemyType;
        public Vector3 Position;
        public int GoldDropped;
        public int ExperienceGained;
    }

    public struct ItemCollectedEvent : IGameEvent
    {
        public string ItemId;
        public string ItemName;
        public ItemRarity Rarity;
    }

    public struct WeaponFiredEvent : IGameEvent
    {
        public string WeaponId;
        public Vector3 Position;
        public Vector3 Direction;
    }

    public struct LevelUpEvent : IGameEvent
    {
        public int NewLevel;
        public int UpgradeChoices;
    }

    public struct GoldChangedEvent : IGameEvent
    {
        public int CurrentGold;
        public int Change;
    }

    public struct WaveStartedEvent : IGameEvent
    {
        public int WaveNumber;
        public int EnemyCount;
    }

    public struct WaveCompletedEvent : IGameEvent
    {
        public int WaveNumber;
        public float TimeTaken;
    }

    public struct UpgradeSelectedEvent : IGameEvent
    {
        public string UpgradeId;
        public int NewLevel;
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}
