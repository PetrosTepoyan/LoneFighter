using System;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Meta
{
    // MetaProgression is the single-scene-spanning singleton that watches the active run,
    // converts a run's end into a RunRecord, mutates AllTimeStats, and asks UnlockManager
    // what (if anything) was just unlocked.
    //
    // It hooks GameManager events; it does NOT mutate GameManager, PlayerLevel, or PlayerHealth.
    // External systems that know about per-run picks (WeaponInventory, UpgradeService) are
    // expected to call RecordWeaponCarried / RecordUpgradePicked / SetPeakLevel as they happen.
    public class MetaProgression : MonoBehaviour
    {
        public static MetaProgression Instance { get; private set; }

        // Listeners (UnlockToast in particular) subscribe here. Fires once per newly unlocked
        // rule, in evaluation order, immediately after RecordRunEnd persists.
        public event Action<UnlockRule> OnNewUnlock;

        // Per-run trackers. Reset whenever GameState transitions into Playing.
        private int _runKills;
        private int _peakLevel;
        private float _runStartTime;
        private readonly List<string> _runWeapons = new List<string>();
        private readonly List<string> _runUpgrades = new List<string>();

        // Cause-of-death hint. Anything that kills the player can stamp this before death
        // (e.g. "Tank contact") via SetDeathCause; otherwise we record a generic string.
        private string _pendingDeathCause = string.Empty;

        // Cached so we can unsubscribe cleanly even if GameManager.Instance flips to null.
        private GameManager _trackedGameManager;
        private bool _runActive;
        private const int RunHistoryCap = 50;

        private const int MaxRunWeapons = 16;
        private const int MaxRunUpgrades = 64;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Touch SaveService so the file is loaded before any other system asks for it.
            _ = SaveService.Current;

            TrySubscribe();
        }

        private void Start()
        {
            // GameManager.Awake may run after ours depending on scene order. Try again on Start
            // (and we keep retrying in Update until we have a hook).
            TrySubscribe();
        }

        private void Update()
        {
            if (_trackedGameManager == null) TrySubscribe();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm == _trackedGameManager) return;

            Unsubscribe();
            _trackedGameManager = gm;
            gm.OnKillsChanged += HandleKillsChanged;
            gm.OnStateChanged += HandleStateChanged;
        }

        private void Unsubscribe()
        {
            if (_trackedGameManager == null) return;
            _trackedGameManager.OnKillsChanged -= HandleKillsChanged;
            _trackedGameManager.OnStateChanged -= HandleStateChanged;
            _trackedGameManager = null;
        }

        private void HandleKillsChanged(int kills)
        {
            _runKills = kills;
        }

        private void HandleStateChanged(GameState prev, GameState next)
        {
            switch (next)
            {
                case GameState.Playing:
                    // BeginRun emits Boot -> Playing; LevelUp -> Playing is just resume.
                    if (prev != GameState.LevelUp && prev != GameState.Paused)
                    {
                        BeginRun();
                    }
                    break;
                case GameState.GameOver:
                    if (_runActive) RecordRunEnd(victory: false);
                    break;
                case GameState.Victory:
                    if (_runActive) RecordRunEnd(victory: true);
                    break;
            }
        }

        private void BeginRun()
        {
            _runKills = 0;
            _peakLevel = 1;
            _runStartTime = Time.unscaledTime;
            _runWeapons.Clear();
            _runUpgrades.Clear();
            _pendingDeathCause = string.Empty;
            _runActive = true;

            var data = SaveService.Current;
            if (data != null && data.profile != null)
            {
                data.profile.runsStarted++;
                // Cheap write; if the player force-quits mid-run we still know they started.
                SaveService.Save();
            }
        }

        // --- public per-run hooks ----------------------------------------------------------

        public void RecordWeaponCarried(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return;
            if (_runWeapons.Count >= MaxRunWeapons) return;
            if (!_runWeapons.Contains(weaponName)) _runWeapons.Add(weaponName);
        }

        public void RecordUpgradePicked(string upgradeName)
        {
            if (string.IsNullOrEmpty(upgradeName)) return;
            if (_runUpgrades.Count >= MaxRunUpgrades) return;
            // Allow duplicates — stack count is meaningful for end-of-run review.
            _runUpgrades.Add(upgradeName);
        }

        public void SetPeakLevel(int level)
        {
            if (level > _peakLevel) _peakLevel = level;
        }

        public void SetDeathCause(string cause)
        {
            if (!string.IsNullOrEmpty(cause)) _pendingDeathCause = cause;
        }

        // --- run finalization --------------------------------------------------------------

        private void RecordRunEnd(bool victory)
        {
            _runActive = false;

            var gm = GameManager.Instance;
            float survived = gm != null ? gm.ElapsedSeconds : Mathf.Max(0f, Time.unscaledTime - _runStartTime);
            int finalKills = gm != null ? gm.Kills : _runKills;

            var record = new RunRecord
            {
                timestampUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                survivedSeconds = survived,
                kills = finalKills,
                peakLevel = Mathf.Max(1, _peakLevel),
                victory = victory,
                deathCause = victory ? "victory" : (string.IsNullOrEmpty(_pendingDeathCause) ? "unknown" : _pendingDeathCause),
                weaponsCarried = new List<string>(_runWeapons),
                upgradesPicked = new List<string>(_runUpgrades),
            };

            var data = SaveService.Current;
            if (data == null) return;

            if (data.runHistory == null) data.runHistory = new List<RunRecord>();
            data.runHistory.Add(record);
            // FIFO drop oldest until we are within cap.
            while (data.runHistory.Count > RunHistoryCap)
            {
                data.runHistory.RemoveAt(0);
            }

            if (data.totals == null) data.totals = new AllTimeStats();
            data.totals.totalKills += record.kills;
            data.totals.totalPlayTimeSec += record.survivedSeconds;
            data.totals.totalRuns += 1;
            if (victory) data.totals.totalVictories += 1;
            if (record.survivedSeconds > data.totals.longestSurvival)
            {
                data.totals.longestSurvival = record.survivedSeconds;
            }
            if (record.kills > data.totals.bestKillStreak)
            {
                data.totals.bestKillStreak = record.kills;
            }

            if (data.profile != null && victory) data.profile.runsWon += 1;

            // Evaluate AFTER stats are updated so rules like "Marathoner" can see the
            // contribution of this very run.
            var newlyUnlocked = UnlockManager.Evaluate(record, data.totals);

            SaveService.Save();

            // Fire after save so listeners observing the unlock can also re-read disk if they want.
            if (newlyUnlocked != null)
            {
                for (int i = 0; i < newlyUnlocked.Count; i++)
                {
                    try { OnNewUnlock?.Invoke(newlyUnlocked[i]); }
                    catch (Exception ex) { Debug.LogWarning($"[MetaProgression] OnNewUnlock listener threw: {ex.Message}"); }
                }
            }
        }
    }
}
