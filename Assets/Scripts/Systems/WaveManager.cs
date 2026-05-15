using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Enemies;

namespace LoneFighter.Systems
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private WaveConfig config;
        [SerializeField] private EnemySpawner spawner;

        private readonly Dictionary<int, float> _accumulators = new();
        private float _runTime;
        private bool _victoryDispatched;

        public WaveConfig Config => config;

        private void OnEnable()
        {
            _runTime = 0f;
            _accumulators.Clear();
            _victoryDispatched = false;
        }

        public void SetConfig(WaveConfig newConfig)
        {
            config = newConfig;
            _accumulators.Clear();
        }

        private void Update()
        {
            if (config == null || spawner == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            _runTime += Time.deltaTime;

            for (int i = 0; i < config.entries.Count; i++)
            {
                var entry = config.entries[i];
                if (_runTime < entry.startTime) continue;
                if (entry.endTime > 0f && _runTime > entry.endTime) continue;
                if (entry.enemy == null || entry.spawnRate <= 0f) continue;

                if (entry.concurrentCap > 0 && spawner.CountOfType(entry.enemy) >= entry.concurrentCap) continue;

                _accumulators.TryGetValue(i, out var acc);
                acc += Time.deltaTime * entry.spawnRate;

                while (acc >= 1f)
                {
                    if (!spawner.TrySpawn(entry.enemy)) break;
                    acc -= 1f;
                }
                _accumulators[i] = acc;
            }

            if (!_victoryDispatched && config.runDuration > 0f && _runTime >= config.runDuration)
            {
                _victoryDispatched = true;
                if (GameManager.Instance != null) GameManager.Instance.TriggerVictory();
            }
        }
    }
}
