using System;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Effects.DeathFx
{
    /// Singleton that listens for enemy deaths and plays the matching `DeathFxProfile`.
    ///
    /// We can't modify `EnemyBase` / `EnemyHealth` (existing-file freeze), so the strategy
    /// is:
    ///   1. Poll `EnemyRegistry.Enemies` periodically (cheap — it's an in-memory list).
    ///   2. For every enemy we haven't seen before, attach a `DeathFxReporter` companion.
    ///   3. The reporter subscribes to `EnemyHealth.OnDied` and forwards the dying enemy
    ///      back here via an Action callback.
    ///   4. Dispatcher resolves a `DeathFxProfile` from the `DeathFxRegistry` keyed by
    ///      `EnemyData.displayName` and triggers particles + gibs + sound + shake +
    ///      hit-stop via the existing FxService / AudioManager / GibSpawner singletons.
    ///
    /// The reporter is attached once per enemy GameObject and survives pool reuse —
    /// `DisallowMultipleComponent` plus the dispatcher's "have we attached one yet?"
    /// check keeps it idempotent.
    public class DeathFxDispatcher : MonoBehaviour
    {
        public static DeathFxDispatcher Instance { get; private set; }

        [Header("Registry")]
        [Tooltip("Maps enemy `displayName` → DeathFxProfile. Required for any per-archetype FX.")]
        [SerializeField] private DeathFxRegistry registry;

        [Header("Polling")]
        [Tooltip("Seconds between sweeps of EnemyRegistry to attach reporters to new enemies. " +
                 "0.05s catches an enemy before it can die from a same-frame hit at peak ~6/sec spawn rate. " +
                 "Attach is O(n) over the registry, which is cheap.")]
        [Range(0.02f, 1f)] [SerializeField] private float pollInterval = 0.05f;

        [Header("Fallbacks")]
        [Tooltip("If true and no profile matches, the dispatcher still calls FxService.EnemyExplosion " +
                 "+ LightShake so death always has *some* feedback.")]
        [SerializeField] private bool fallbackToFxServiceDefaults = true;

        [Header("Debug")]
        [SerializeField] private bool logDispatches = false;

        // Tracks the EnemyBase instances we've already attached a reporter to.
        // We never iterate this set — only Add / Contains / occasional Remove on a sweep
        // that finds destroyed entries — so HashSet<> is the right choice.
        private readonly HashSet<EnemyBase> _attached = new HashSet<EnemyBase>();
        // Stale-entry scratch buffer reused across sweeps to keep alloc traffic at zero.
        private readonly List<EnemyBase> _staleScratch = new List<EnemyBase>(16);

        private float _pollTimer;
        // Stable lambda we re-use as the reporter callback — avoids allocating one per attach.
        private Action<EnemyBase> _onReporterFired;

        public DeathFxRegistry Registry => registry;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _onReporterFired = OnEnemyDied;
        }

        private void Start()
        {
            // Sweep once at boot in case enemies are already registered before this
            // dispatcher Awakens (e.g. spawned by an earlier-Awake script in the same scene).
            ScanRegistry();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _attached.Clear();
        }

        private void Update()
        {
            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < pollInterval) return;
            _pollTimer = 0f;
            ScanRegistry();
        }

        // -----------------------------------------------------------------------------------
        // Polling: attach DeathFxReporter to any enemy we haven't seen.
        // -----------------------------------------------------------------------------------

        private void ScanRegistry()
        {
            var enemies = EnemyRegistry.Enemies;
            if (enemies == null) return;

            // Pass 1: attach reporters to any new enemies in the registry.
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                if (_attached.Contains(e)) continue;
                AttachReporter(e);
                _attached.Add(e);
            }

            // Pass 2: prune destroyed entries from our tracking set. Unity overrides `==`
            // for destroyed UnityEngine.Objects, so `e == null` is the right liveness check.
            _staleScratch.Clear();
            foreach (var e in _attached)
            {
                if (e == null) _staleScratch.Add(e);
            }
            for (int i = 0; i < _staleScratch.Count; i++) _attached.Remove(_staleScratch[i]);
        }

        private void AttachReporter(EnemyBase enemy)
        {
            // DisallowMultipleComponent + GetComponent guard make this idempotent across
            // pool reuse — the same GameObject keeps its reporter forever. We always
            // re-set the callback so a fresh dispatcher (e.g. after a scene reload)
            // takes ownership of a reporter that was attached by the previous one.
            var reporter = enemy.GetComponent<DeathFxReporter>();
            if (reporter == null) reporter = enemy.gameObject.AddComponent<DeathFxReporter>();
            reporter.ReportDeath = _onReporterFired;
        }

        // -----------------------------------------------------------------------------------
        // The death event itself.
        // -----------------------------------------------------------------------------------

        private void OnEnemyDied(EnemyBase enemy)
        {
            if (enemy == null) return;
            var data = enemy.Data;
            string displayName = data != null ? data.displayName : null;
            Vector3 position = enemy.transform.position;

            DeathFxProfile profile = registry != null ? registry.Resolve(displayName) : null;

            if (logDispatches)
            {
                Debug.Log($"[DeathFxDispatcher] {(displayName ?? "<unknown>")} died — " +
                          $"profile={(profile != null ? profile.label : "<none>")}");
            }

            if (profile == null)
            {
                if (fallbackToFxServiceDefaults) PlayFxServiceDefaults(position);
                return;
            }

            PlayProfile(profile, position);
        }

        private void PlayProfile(DeathFxProfile profile, Vector3 position)
        {
            // ----- Particles -----
            if (profile.particlePrefab != null)
            {
                SpawnTintedBurst(profile, position);
            }
            else if (fallbackToFxServiceDefaults && FxService.Instance != null)
            {
                FxService.Instance.EnemyExplosion(position);
            }

            // ----- Sound -----
            if (!string.IsNullOrWhiteSpace(profile.soundCueName) && AudioManager.Instance != null)
            {
                if (Enum.TryParse(profile.soundCueName.Trim(), ignoreCase: true, out AudioCue cue))
                {
                    AudioManager.Instance.Play(cue, Mathf.Clamp01(profile.soundVolumeScale));
                }
                else
                {
                    AudioManager.Instance.Play(AudioCue.EnemyDeath, Mathf.Clamp01(profile.soundVolumeScale));
                }
            }

            // ----- Camera shake -----
            if (FxService.Instance != null)
            {
                switch (profile.shake)
                {
                    case DeathFxShake.Light: FxService.Instance.LightShake(); break;
                    case DeathFxShake.Heavy: FxService.Instance.HeavyShake(); break;
                    case DeathFxShake.None: break;
                }
            }

            // ----- Hit-stop -----
            if (profile.hitStopSeconds > 0f && FxService.Instance != null)
            {
                FxService.Instance.HitStop(profile.hitStopSeconds);
            }

            // ----- Gibs -----
            if (profile.gibCount > 0 && GibSpawner.Instance != null)
            {
                GibSpawner.Instance.Spawn(
                    origin: position,
                    count: profile.gibCount,
                    tint: profile.gibColor,
                    sizeRange: profile.gibSizeRange,
                    speedRange: profile.gibSpeedRange,
                    lifetime: profile.gibLifetime);
            }
        }

        private void PlayFxServiceDefaults(Vector3 position)
        {
            if (FxService.Instance == null) return;
            FxService.Instance.EnemyExplosion(position);
            FxService.Instance.LightShake();
        }

        /// Plays the profile's particle prefab via PoolService (mirroring FxService.Spawn),
        /// then optionally tints / resizes / re-bursts the just-played ParticleSystem so a
        /// single shared prefab can paint multiple archetypes.
        private void SpawnTintedBurst(DeathFxProfile profile, Vector3 position)
        {
            GameObject instance;
            if (PoolService.Instance != null)
            {
                instance = PoolService.Instance.Get(profile.particlePrefab, position, Quaternion.identity);
            }
            else
            {
                instance = UnityEngine.Object.Instantiate(profile.particlePrefab, position, Quaternion.identity);
            }
            if (instance == null) return;

            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                // Tint override — alpha 0 means "leave the prefab's color alone".
                if (profile.particleTint.a > 0f)
                {
                    var m = ps.main;
                    m.startColor = profile.particleTint;
                    ps.main = m;
                }
                // Size override — multiplier hits every curve mode (Constant / RandomBetween /
                // Curve), so we don't have to inspect the MinMaxCurveMode.
                if (!Mathf.Approximately(profile.particleSizeMultiplier, 1f))
                {
                    var m = ps.main;
                    m.startSizeMultiplier *= profile.particleSizeMultiplier;
                    ps.main = m;
                }
                // Burst count override. Burst.count is a MinMaxCurve; wrapping the int in
                // a Constant MinMaxCurve avoids the int→short conversion warning on the
                // Burst constructor overloads.
                if (profile.particleBurstCount > 0)
                {
                    var em = ps.emission;
                    var newCount = new ParticleSystem.MinMaxCurve((float)profile.particleBurstCount);
                    if (em.burstCount > 0)
                    {
                        var burst = em.GetBurst(0);
                        burst.count = newCount;
                        em.SetBurst(0, burst);
                    }
                    else
                    {
                        em.SetBursts(new[] { new ParticleSystem.Burst(0f, newCount) });
                    }
                }
                ps.Clear(true);
                ps.Play(true);
            }
            if (instance.GetComponent<ParticleAutoRelease>() == null)
            {
                instance.AddComponent<ParticleAutoRelease>();
            }
        }
    }
}
