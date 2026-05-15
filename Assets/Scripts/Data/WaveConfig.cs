using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Data
{
    [Serializable]
    public struct WaveEntry
    {
        [Tooltip("Seconds from run start when this entry becomes active.")]
        public float startTime;
        [Tooltip("Seconds from run start when this entry stops contributing spawns.")]
        public float endTime;
        public EnemyData enemy;
        [Tooltip("Enemies spawned per second while active.")]
        public float spawnRate;
        [Tooltip("Hard cap on simultaneous enemies of this type. 0 = no cap.")]
        public int concurrentCap;
    }

    [CreateAssetMenu(menuName = "LoneFighter/Wave Config", fileName = "WaveConfig")]
    public class WaveConfig : ScriptableObject
    {
        [Tooltip("Total run length in seconds. Survive to win.")]
        public float runDuration = 300f;
        public List<WaveEntry> entries = new();
    }
}
