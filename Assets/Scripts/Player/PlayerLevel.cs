using System;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Player
{
    public class PlayerLevel : MonoBehaviour
    {
        [SerializeField] private int baseXpToLevel = 5;
        [SerializeField] private float xpGrowthPerLevel = 1.35f;
        [SerializeField, Range(0.1f, 10f)] private float xpMultiplier = 1f;

        public int Level { get; private set; } = 1;
        public int CurrentXp { get; private set; }
        public int XpToNext { get; private set; }

        public event Action<int> OnLevelUp;
        public event Action<int, int> OnXpChanged;

        public float XpMultiplier
        {
            get => xpMultiplier;
            set => xpMultiplier = Mathf.Max(0.1f, value);
        }

        private void Awake()
        {
            XpToNext = baseXpToLevel;
        }

        private void Start()
        {
            OnXpChanged?.Invoke(CurrentXp, XpToNext);
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            int scaled = Mathf.Max(1, Mathf.RoundToInt(amount * xpMultiplier));
            CurrentXp += scaled;

            while (CurrentXp >= XpToNext)
            {
                CurrentXp -= XpToNext;
                Level++;
                XpToNext = Mathf.Max(1, Mathf.RoundToInt(baseXpToLevel * Mathf.Pow(xpGrowthPerLevel, Level - 1)));
                OnLevelUp?.Invoke(Level);
                if (FxService.Instance != null)
                {
                    FxService.Instance.LevelUpBurst(transform.position);
                    FxService.Instance.HitStop(0.06f);
                }
                if (GameManager.Instance != null) GameManager.Instance.EnterLevelUp();
            }
            OnXpChanged?.Invoke(CurrentXp, XpToNext);
        }
    }
}
