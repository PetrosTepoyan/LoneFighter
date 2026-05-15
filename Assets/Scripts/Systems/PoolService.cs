using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace LoneFighter.Systems
{
    public class PoolService : MonoBehaviour
    {
        public static PoolService Instance { get; private set; }

        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new();

        [SerializeField] private int defaultCapacity = 32;
        [SerializeField] private int maxSize = 1024;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var pool = GetOrCreatePool(prefab);
            var instance = pool.Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            _instanceToPrefab[instance] = prefab;
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;
            if (!_instanceToPrefab.TryGetValue(instance, out var prefab) || !_pools.TryGetValue(prefab, out var pool))
            {
                Destroy(instance);
                return;
            }
            pool.Release(instance);
        }

        private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out var pool)) return pool;

            pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = Instantiate(prefab, transform);
                    return go;
                },
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go => go.SetActive(false),
                actionOnDestroy: go =>
                {
                    if (go != null) Destroy(go);
                },
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);

            _pools[prefab] = pool;
            return pool;
        }
    }
}
