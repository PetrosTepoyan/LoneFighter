using System;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Polish
{
    /// Sliding-window kill combo. Drives ComboHud and grants escalating bonus XP at thresholds.
    /// Reads kill events from GameManager.OnKillsChanged and uses Time.time for the window.
    public class ComboCounter : MonoBehaviour
    {
        public static ComboCounter Instance { get; private set; }

        [Header("Combo Window")]
        [SerializeField, Min(0.5f)] private float windowSeconds = 3f;

        [Header("Bonus XP Thresholds (must be ascending)")]
        [Tooltip("Awards bonusXpPerTier[i] when Current reaches thresholds[i].")]
        [SerializeField] private int[] thresholds = { 5, 10, 25, 50 };
        [SerializeField] private int[] bonusXpPerTier = { 1, 3, 8, 20 };

        public int Current { get; private set; }
        public int Best { get; private set; }

        /// Fires whenever the combo count changes (kill landed, combo expired).
        public event Action<int> OnComboChanged;
        /// Fires for each kill while the combo is active — payload is the enemy type display name (may be empty).
        public event Action<string> OnKillRecorded;

        private readonly Queue<float> _hits = new();
        private readonly HashSet<int> _awardedThisRunForCombo = new();
        private int _lastObservedKillCount = -1;
        private PlayerLevel _level;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (GameManager.Instance != null) GameManager.Instance.OnKillsChanged -= HandleKillsChanged;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                _lastObservedKillCount = GameManager.Instance.Kills;
                GameManager.Instance.OnKillsChanged += HandleKillsChanged;
            }
            if (PlayerController.Instance != null)
            {
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
        }

        private void HandleKillsChanged(int totalKills)
        {
            int delta = totalKills - _lastObservedKillCount;
            if (_lastObservedKillCount < 0) delta = 0;
            _lastObservedKillCount = totalKills;

            if (delta <= 0)
            {
                // Run reset — clear combo state.
                _hits.Clear();
                _awardedThisRunForCombo.Clear();
                if (Current != 0)
                {
                    Current = 0;
                    OnComboChanged?.Invoke(Current);
                }
                return;
            }

            float now = Time.time;
            for (int i = 0; i < delta; i++)
            {
                _hits.Enqueue(now);
            }
            Trim(now);
            UpdateAndNotify();
            // Notify listeners (KillFeedHud) for each new kill.
            string name = TryGetMostRecentEnemyName();
            for (int i = 0; i < delta; i++)
            {
                OnKillRecorded?.Invoke(name);
            }
            CheckThresholds();
        }

        private void Update()
        {
            if (_hits.Count == 0) return;
            float now = Time.time;
            int before = _hits.Count;
            Trim(now);
            if (_hits.Count != before)
            {
                UpdateAndNotify();
                if (Current == 0) _awardedThisRunForCombo.Clear();
            }
        }

        private void Trim(float now)
        {
            while (_hits.Count > 0 && now - _hits.Peek() > windowSeconds)
            {
                _hits.Dequeue();
            }
        }

        private void UpdateAndNotify()
        {
            int newCurrent = _hits.Count;
            if (newCurrent == Current) return;
            Current = newCurrent;
            if (Current > Best) Best = Current;
            OnComboChanged?.Invoke(Current);
        }

        private void CheckThresholds()
        {
            if (thresholds == null || bonusXpPerTier == null) return;
            int n = Mathf.Min(thresholds.Length, bonusXpPerTier.Length);
            for (int i = 0; i < n; i++)
            {
                int t = thresholds[i];
                if (Current >= t && !_awardedThisRunForCombo.Contains(t))
                {
                    _awardedThisRunForCombo.Add(t);
                    int xp = Mathf.Max(0, bonusXpPerTier[i]);
                    if (xp > 0 && _level != null) _level.AddXp(xp);
                }
            }
        }

        private static string TryGetMostRecentEnemyName()
        {
            // Best-effort: the enemy is already despawned by the time we hear the kill event,
            // so we sample a still-alive sibling's display name as a proxy. KillFeedHud uses
            // this purely for flavor; empty is fine.
            var list = EnemyRegistry.Enemies;
            if (list == null || list.Count == 0) return string.Empty;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e != null && e.Data != null) return e.Data.displayName;
            }
            return string.Empty;
        }
    }
}
