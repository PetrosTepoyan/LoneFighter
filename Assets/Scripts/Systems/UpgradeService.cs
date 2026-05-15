using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.Systems
{
    public class UpgradeService : MonoBehaviour
    {
        public static UpgradeService Instance { get; private set; }

        [SerializeField] private List<UpgradeData> pool = new();

        private readonly Dictionary<UpgradeData, int> _stacks = new();

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

        public List<UpgradeData> Roll(int count)
        {
            var eligible = new List<UpgradeData>();
            foreach (var u in pool)
            {
                if (u == null) continue;
                _stacks.TryGetValue(u, out var taken);
                if (taken >= u.maxStacks) continue;
                eligible.Add(u);
            }

            var picks = new List<UpgradeData>(count);
            for (int i = 0; i < count && eligible.Count > 0; i++)
            {
                float totalWeight = 0f;
                foreach (var u in eligible) totalWeight += u.weight;

                float r = Random.value * totalWeight;
                int chosen = 0;
                for (int j = 0; j < eligible.Count; j++)
                {
                    r -= eligible[j].weight;
                    if (r <= 0f) { chosen = j; break; }
                }

                picks.Add(eligible[chosen]);
                eligible.RemoveAt(chosen);
            }

            return picks;
        }

        public void RecordChoice(UpgradeData upgrade)
        {
            if (upgrade == null) return;
            _stacks.TryGetValue(upgrade, out var count);
            _stacks[upgrade] = count + 1;
        }

        public int StackCount(UpgradeData upgrade)
        {
            if (upgrade == null) return 0;
            _stacks.TryGetValue(upgrade, out var count);
            return count;
        }

        public void ResetRun()
        {
            _stacks.Clear();
        }
    }
}
