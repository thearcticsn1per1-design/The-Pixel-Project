using UnityEngine;
using System.Collections.Generic;

namespace PixelProject.Utilities
{
    /// <summary>
    /// Collection of utility methods and extension methods.
    /// </summary>
    public static class Helpers
    {
        private static Camera mainCamera;

        /// <summary>
        /// Cached main camera reference for performance.
        /// </summary>
        public static Camera MainCamera
        {
            get
            {
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                }
                return mainCamera;
            }
        }

        /// <summary>
        /// Gets the mouse position in world coordinates.
        /// </summary>
        public static Vector3 GetMouseWorldPosition()
        {
            if (MainCamera == null) return Vector3.zero;

            Vector3 mousePos = MainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            return mousePos;
        }

        /// <summary>
        /// Gets the angle between two points in degrees.
        /// </summary>
        public static float GetAngle(Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Converts angle in degrees to a direction vector.
        /// </summary>
        public static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        /// <summary>
        /// Checks if a point is within the camera view.
        /// </summary>
        public static bool IsPointInView(Vector3 worldPoint, float buffer = 0f)
        {
            if (MainCamera == null) return false;

            Vector3 viewportPoint = MainCamera.WorldToViewportPoint(worldPoint);
            return viewportPoint.x >= -buffer && viewportPoint.x <= 1 + buffer &&
                   viewportPoint.y >= -buffer && viewportPoint.y <= 1 + buffer &&
                   viewportPoint.z > 0;
        }

        /// <summary>
        /// Gets a random point within a circle.
        /// </summary>
        public static Vector2 RandomPointInCircle(Vector2 center, float radius)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, radius);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        /// <summary>
        /// Gets a random point on the edge of a circle.
        /// </summary>
        public static Vector2 RandomPointOnCircle(Vector2 center, float radius)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>
        /// Shuffles a list in place using Fisher-Yates algorithm.
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Gets a random element from a list.
        /// </summary>
        public static T RandomElement<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// Remaps a value from one range to another.
        /// </summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        }

        /// <summary>
        /// Smoothly approaches a target value.
        /// </summary>
        public static float Approach(float current, float target, float maxDelta)
        {
            if (current < target)
            {
                return Mathf.Min(current + maxDelta, target);
            }
            else
            {
                return Mathf.Max(current - maxDelta, target);
            }
        }

        /// <summary>
        /// Checks if a layer mask contains a specific layer.
        /// </summary>
        public static bool ContainsLayer(this LayerMask mask, int layer)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        /// <summary>
        /// Destroys all children of a transform.
        /// </summary>
        public static void DestroyChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Sets the layer of a GameObject and all its children.
        /// </summary>
        public static void SetLayerRecursively(this GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                child.gameObject.SetLayerRecursively(layer);
            }
        }

        /// <summary>
        /// Gets or adds a component to a GameObject.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// Formats a time value as MM:SS.
        /// </summary>
        public static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        /// <summary>
        /// Formats a large number with K/M suffixes.
        /// </summary>
        public static string FormatNumber(float number)
        {
            if (number >= 1000000)
            {
                return $"{number / 1000000f:F1}M";
            }
            else if (number >= 1000)
            {
                return $"{number / 1000f:F1}K";
            }
            return number.ToString("F0");
        }
    }

    /// <summary>
    /// Simple timer utility class.
    /// </summary>
    [System.Serializable]
    public class Timer
    {
        public float Duration { get; private set; }
        public float RemainingTime { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsComplete => RemainingTime <= 0 && IsRunning;
        public float Progress => Duration > 0 ? 1f - (RemainingTime / Duration) : 0f;

        public Timer(float duration)
        {
            Duration = duration;
            RemainingTime = duration;
            IsRunning = false;
        }

        public void Start()
        {
            RemainingTime = Duration;
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Reset()
        {
            RemainingTime = Duration;
        }

        public void Update(float deltaTime)
        {
            if (!IsRunning) return;

            RemainingTime -= deltaTime;
            if (RemainingTime < 0)
            {
                RemainingTime = 0;
            }
        }
    }

    /// <summary>
    /// Cooldown timer that can be reset.
    /// </summary>
    [System.Serializable]
    public class Cooldown
    {
        public float Duration { get; private set; }
        public float RemainingTime { get; private set; }
        public bool IsReady => RemainingTime <= 0;
        public float Progress => Duration > 0 ? 1f - (RemainingTime / Duration) : 1f;

        public Cooldown(float duration)
        {
            Duration = duration;
            RemainingTime = 0;
        }

        public bool TryUse()
        {
            if (IsReady)
            {
                RemainingTime = Duration;
                return true;
            }
            return false;
        }

        public void Update(float deltaTime)
        {
            if (RemainingTime > 0)
            {
                RemainingTime -= deltaTime;
            }
        }

        public void Reset()
        {
            RemainingTime = 0;
        }

        public void SetDuration(float newDuration)
        {
            Duration = newDuration;
        }
    }
}
