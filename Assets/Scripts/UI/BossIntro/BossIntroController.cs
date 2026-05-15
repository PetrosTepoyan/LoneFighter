using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.UI.BossIntro
{
    /// <summary>
    /// Scene-resident singleton that watches <see cref="EnemyRegistry.Enemies"/>
    /// once per second for newly-spawned enemies whose <c>Data.displayName</c>
    /// matches a <see cref="BossMeta"/> entry in <see cref="BossMetaRegistry.Active"/>.
    /// On the very first sighting of a given boss it kicks off the cinematic
    /// <see cref="BossIntroSequence"/> coroutine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The poll cadence is intentionally coarse — the goal is to react to
    /// spawn events without coupling to any specific spawner. <see cref="EnemyRegistry"/>
    /// is already the single source of truth for "what is alive right now",
    /// so polling it is robust against future spawn paths (waves, scripted
    /// triggers, debug tools, etc.).
    /// </para>
    /// <para>
    /// Each boss is announced at most once per scene load. The <see cref="_seenNames"/>
    /// set persists for the lifetime of the controller and is cleared whenever
    /// the controller's <c>OnEnable</c> runs (i.e. when the gameplay scene reloads).
    /// </para>
    /// <para>
    /// All timing inside the sequence uses unscaled time so the cinematic still
    /// reads correctly even though it manipulates <c>Time.timeScale</c>.
    /// </para>
    /// </remarks>
    public class BossIntroController : MonoBehaviour
    {
        public static BossIntroController Instance { get; private set; }

        [Header("Wiring")]
        [Tooltip("Registry asset. If left null the controller uses BossMetaRegistry.Active (set by the first loaded asset).")]
        [SerializeField] private BossMetaRegistry registry;

        [Tooltip("Banner that slides in with the boss name. Optional — sequence skips banner steps if null.")]
        [SerializeField] private BossNameBanner banner;

        [Tooltip("Dark-vignette spotlight that highlights the boss. Optional — sequence skips spotlight steps if null.")]
        [SerializeField] private BossSpotlight spotlight;

        [Tooltip("Optional stinger player. Falls back to a runtime-added component on this object if null.")]
        [SerializeField] private BossStingerPlayer stinger;

        [Header("Polling")]
        [Tooltip("Seconds between EnemyRegistry scans. The default 1.0s is enough — bosses are rare.")]
        [SerializeField] private float pollInterval = 1.0f;

        [Tooltip("If true, the controller starts polling immediately on enable.")]
        [SerializeField] private bool autoStart = true;

        private readonly HashSet<string> _seenNames = new HashSet<string>();
        private Coroutine _pollRoutine;
        private Coroutine _activeIntro;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure we have a stinger player even if the inspector slot is empty.
            if (stinger == null) stinger = GetComponent<BossStingerPlayer>();
            if (stinger == null) stinger = gameObject.AddComponent<BossStingerPlayer>();
        }

        private void OnEnable()
        {
            _seenNames.Clear();
            if (registry != null) BossMetaRegistry.SetActive(registry);
            if (autoStart) BeginPolling();
        }

        private void OnDisable()
        {
            StopPolling();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Start the poll loop. Safe to call when already polling — it is a no-op.</summary>
        public void BeginPolling()
        {
            if (_pollRoutine != null) return;
            _pollRoutine = StartCoroutine(PollLoop());
        }

        /// <summary>Stop the poll loop. Does not cancel an in-flight intro sequence.</summary>
        public void StopPolling()
        {
            if (_pollRoutine != null)
            {
                StopCoroutine(_pollRoutine);
                _pollRoutine = null;
            }
        }

        /// <summary>Clear the seen-names set so the next sighting triggers a fresh intro.</summary>
        public void ResetSeen() => _seenNames.Clear();

        /// <summary>
        /// Manually trigger an intro for a specific boss meta. Useful for debug menus
        /// or scripted story moments. Bypasses the seen-set so the same boss can be
        /// re-announced.
        /// </summary>
        public void TriggerManually(BossMeta meta, Transform target = null)
        {
            if (meta == null) return;
            StartIntro(meta, target);
        }

        private IEnumerator PollLoop()
        {
            // First scan is immediate so a boss already alive at scene-load is caught
            // without waiting a full poll interval. Subsequent scans wait the interval.
            while (true)
            {
                Scan();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, pollInterval));
            }
        }

        private void Scan()
        {
            var alive = EnemyRegistry.Enemies;
            if (alive == null || alive.Count == 0) return;

            for (int i = 0; i < alive.Count; i++)
            {
                var e = alive[i];
                if (e == null) continue;
                var data = e.Data;
                if (data == null) continue;

                string name = data.displayName;
                if (string.IsNullOrEmpty(name)) continue;
                if (_seenNames.Contains(name)) continue;

                var meta = BossMetaRegistry.Get(name);
                if (meta == null) continue;

                // Mark seen up-front so duplicate detections within the same scan loop
                // don't double-fire if the registry has the same boss appear twice.
                _seenNames.Add(name);
                StartIntro(meta, e.transform);
            }
        }

        private void StartIntro(BossMeta meta, Transform target)
        {
            // Only one intro can run at a time. If something is already playing, skip the new one
            // entirely — chaining cinematics on top of each other would feel chaotic, not climactic.
            if (_activeIntro != null) return;
            _activeIntro = StartCoroutine(RunIntro(meta, target));
        }

        private IEnumerator RunIntro(BossMeta meta, Transform target)
        {
            var seq = new BossIntroSequence(banner, spotlight, stinger);
            yield return seq.Run(meta, target);
            _activeIntro = null;
        }
    }
}
