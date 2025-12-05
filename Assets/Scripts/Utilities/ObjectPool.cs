using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PixelProject.Utilities
{
    /// <summary>
    /// Generic object pooling system for efficient instantiation and reuse of GameObjects.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool autoExpand = true;
        [SerializeField] private int defaultPoolSize = 20;

        [Header("Pre-warm Pools")]
        [SerializeField] private List<PoolPrewarm> prewarmPools = new List<PoolPrewarm>();

        private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
        private Dictionary<GameObject, GameObject> prefabLookup = new Dictionary<GameObject, GameObject>();
        private Dictionary<GameObject, Transform> poolParents = new Dictionary<GameObject, Transform>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Pre-warm pools
            foreach (var prewarm in prewarmPools)
            {
                CreatePool(prewarm.prefab, prewarm.size);
            }
        }

        /// <summary>
        /// Creates a new pool for the given prefab.
        /// </summary>
        public void CreatePool(GameObject prefab, int size)
        {
            if (prefab == null || pools.ContainsKey(prefab)) return;

            // Create parent for organization
            GameObject parentObj = new GameObject($"Pool_{prefab.name}");
            parentObj.transform.SetParent(transform);
            poolParents[prefab] = parentObj.transform;

            // Create pool
            Queue<GameObject> pool = new Queue<GameObject>();

            for (int i = 0; i < size; i++)
            {
                GameObject obj = CreatePooledObject(prefab);
                pool.Enqueue(obj);
            }

            pools[prefab] = pool;
        }

        private GameObject CreatePooledObject(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, poolParents[prefab]);
            obj.SetActive(false);
            prefabLookup[obj] = prefab;

            // Add pooled object component for auto-return
            var pooledObj = obj.GetComponent<PooledObject>();
            if (pooledObj == null)
            {
                pooledObj = obj.AddComponent<PooledObject>();
            }
            pooledObj.Prefab = prefab;

            return obj;
        }

        /// <summary>
        /// Gets an object from the pool, or creates a new one if needed.
        /// </summary>
        public GameObject Get(GameObject prefab)
        {
            if (prefab == null) return null;

            // Create pool if it doesn't exist
            if (!pools.ContainsKey(prefab))
            {
                CreatePool(prefab, defaultPoolSize);
            }

            Queue<GameObject> pool = pools[prefab];

            // Get from pool or create new
            GameObject obj;
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();

                // Make sure it's valid (wasn't destroyed externally)
                if (obj == null)
                {
                    obj = CreatePooledObject(prefab);
                }
            }
            else if (autoExpand)
            {
                obj = CreatePooledObject(prefab);
            }
            else
            {
                Debug.LogWarning($"Pool for {prefab.name} is empty and auto-expand is disabled");
                return null;
            }

            obj.SetActive(true);
            return obj;
        }

        /// <summary>
        /// Gets an object from the pool and positions it.
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject obj = Get(prefab);
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            return obj;
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        public void Return(GameObject obj, GameObject prefab = null)
        {
            if (obj == null) return;

            // Find prefab if not provided
            if (prefab == null)
            {
                if (!prefabLookup.TryGetValue(obj, out prefab))
                {
                    Debug.LogWarning($"Trying to return {obj.name} but couldn't find its prefab. Destroying instead.");
                    Destroy(obj);
                    return;
                }
            }

            obj.SetActive(false);

            // Reset transform
            if (poolParents.TryGetValue(prefab, out Transform parent))
            {
                obj.transform.SetParent(parent);
            }

            // Add back to pool
            if (pools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool.Enqueue(obj);
            }
            else
            {
                // Pool was destroyed, just destroy the object
                Destroy(obj);
            }
        }

        /// <summary>
        /// Returns an object to the pool after a delay.
        /// </summary>
        public void ReturnAfterDelay(GameObject obj, GameObject prefab, float delay)
        {
            StartCoroutine(ReturnDelayed(obj, prefab, delay));
        }

        private IEnumerator ReturnDelayed(GameObject obj, GameObject prefab, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (obj != null)
            {
                Return(obj, prefab);
            }
        }

        /// <summary>
        /// Clears all pools and destroys pooled objects.
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in pools.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj != null)
                    {
                        Destroy(obj);
                    }
                }
            }

            pools.Clear();
            prefabLookup.Clear();

            foreach (var parent in poolParents.Values)
            {
                if (parent != null)
                {
                    Destroy(parent.gameObject);
                }
            }
            poolParents.Clear();
        }

        /// <summary>
        /// Clears a specific pool.
        /// </summary>
        public void ClearPool(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out Queue<GameObject> pool)) return;

            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                {
                    prefabLookup.Remove(obj);
                    Destroy(obj);
                }
            }

            pools.Remove(prefab);

            if (poolParents.TryGetValue(prefab, out Transform parent))
            {
                Destroy(parent.gameObject);
                poolParents.Remove(prefab);
            }
        }

        /// <summary>
        /// Returns all active instances of a prefab to the pool.
        /// </summary>
        public void ReturnAll(GameObject prefab)
        {
            List<GameObject> toReturn = new List<GameObject>();

            foreach (var kvp in prefabLookup)
            {
                if (kvp.Value == prefab && kvp.Key != null && kvp.Key.activeInHierarchy)
                {
                    toReturn.Add(kvp.Key);
                }
            }

            foreach (var obj in toReturn)
            {
                Return(obj, prefab);
            }
        }

        /// <summary>
        /// Gets the current size of a pool.
        /// </summary>
        public int GetPoolSize(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                return pool.Count;
            }
            return 0;
        }
    }

    /// <summary>
    /// Component attached to pooled objects for auto-return functionality.
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        public GameObject Prefab { get; set; }

        [SerializeField] private float autoReturnDelay = -1f; // -1 = no auto-return

        private float activeTimer;

        private void OnEnable()
        {
            activeTimer = 0f;
        }

        private void Update()
        {
            if (autoReturnDelay > 0)
            {
                activeTimer += Time.deltaTime;
                if (activeTimer >= autoReturnDelay)
                {
                    ReturnToPool();
                }
            }
        }

        public void ReturnToPool()
        {
            ObjectPool.Instance?.Return(gameObject, Prefab);
        }

        public void ReturnToPoolAfterDelay(float delay)
        {
            ObjectPool.Instance?.ReturnAfterDelay(gameObject, Prefab, delay);
        }
    }

    [System.Serializable]
    public class PoolPrewarm
    {
        public GameObject prefab;
        public int size = 20;
    }
}
