using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Player;
using LoneFighter.Systems;
using LoneFighter.Utils;

namespace LoneFighter.Enemies
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject xpGemPrefab;
        [Tooltip("Distance from player at which enemies spawn. Should be just off camera.")]
        [SerializeField] private float spawnRadius = 12f;
        [SerializeField] private float spawnRadiusJitter = 2f;
        [Tooltip("Hard cap on simultaneous enemies on screen.")]
        [SerializeField] private int globalCap = 200;

        public int GlobalCap => globalCap;

        public bool TrySpawn(EnemyData data)
        {
            if (data == null || data.prefab == null) return false;
            if (PlayerController.Instance == null) return false;
            if (EnemyRegistry.Count >= globalCap) return false;

            Vector2 center = PlayerController.Instance.transform.position;
            Vector2 pos = MathUtil.RandomPointOnRing(center, spawnRadius, spawnRadius + spawnRadiusJitter);

            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(data.prefab, pos, Quaternion.identity)
                : Instantiate(data.prefab, pos, Quaternion.identity);

            var enemy = instance.GetComponent<EnemyBase>();
            if (enemy != null) enemy.Configure(data, xpGemPrefab);
            return true;
        }

        public int CountOfType(EnemyData data)
        {
            if (data == null) return 0;
            int n = 0;
            var list = EnemyRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Data == data) n++;
            }
            return n;
        }
    }
}
