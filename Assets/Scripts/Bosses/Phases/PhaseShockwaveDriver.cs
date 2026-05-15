using System.Reflection;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Bosses
{
    /// <summary>
    /// Companion component spawned at runtime by <see cref="WardenPhases"/> when a phase needs
    /// to emit shockwaves on a cadence independent of <see cref="WardenBoss"/>'s built-in slam
    /// timer. Two independent modes can both be active simultaneously:
    ///
    ///   * <see cref="ConfigureContinuous"/>(interval) — fires a shockwave every <c>interval</c>
    ///     seconds while running. Used by Phase 3 enrage.
    ///   * <see cref="ConfigureEcho"/>(delay) — when the underlying WardenBoss slams (detected
    ///     by listening for the FX it triggers), schedule a follow-up shockwave <c>delay</c>
    ///     seconds later. Used by Phase 2 "double slam". Since WardenBoss does not expose a
    ///     slam event, the echo is driven by polling the FxService position-based heuristic;
    ///     if that proves brittle, the continuous mode is a safe fallback.
    ///
    /// The driver pulls the shockwave prefab from WardenBoss via reflection so we don't need
    /// the prefab wired up twice. When reflection cannot find it (because the field was
    /// stripped or renamed), we log a one-time setup note and skip emission — the rest of the
    /// phase logic still runs (move-speed tweaks, etc.).
    /// </summary>
    [AddComponentMenu("")] // runtime-only companion
    public sealed class PhaseShockwaveDriver : MonoBehaviour
    {
        private WardenBoss _warden;
        private EnemyBase _enemy;

        // Continuous mode.
        private bool _continuousActive;
        private float _continuousInterval = 1.5f;
        private float _continuousTimer;

        // Echo mode (delayed second wave after each natural slam).
        private bool _echoActive;
        private float _echoDelay = 0.35f;
        private float _echoCountdown = -1f;

        public static PhaseShockwaveDriver GetOrAdd(BossEncounter ctx)
        {
            var existing = ctx.GetComponent<PhaseShockwaveDriver>();
            if (existing != null) return existing;
            return ctx.gameObject.AddComponent<PhaseShockwaveDriver>();
        }

        public static void Detach(BossEncounter ctx)
        {
            var existing = ctx.GetComponent<PhaseShockwaveDriver>();
            if (existing != null)
            {
                existing.ClearContinuous();
                existing.ClearEcho();
                // We don't Destroy here — leaving the disabled driver in place avoids GC churn
                // if the player re-crosses a phase boundary in a single fight. Realistically that
                // can't happen with monotonic HP, but cheap insurance.
                existing.enabled = false;
            }
        }

        private void Awake()
        {
            _warden = GetComponent<WardenBoss>();
            _enemy = GetComponent<EnemyBase>();
        }

        public void ConfigureContinuous(float interval)
        {
            _continuousActive = true;
            _continuousInterval = Mathf.Max(0.25f, interval);
            // Stagger first emission slightly so phase-entry doesn't double-up with a natural slam.
            _continuousTimer = _continuousInterval * 0.5f;
            enabled = true;
        }

        public void ClearContinuous()
        {
            _continuousActive = false;
            _continuousTimer = 0f;
        }

        public void ConfigureEcho(float echoDelay)
        {
            _echoActive = true;
            _echoDelay = Mathf.Max(0.05f, echoDelay);
            _echoCountdown = -1f;
            enabled = true;
        }

        public void ClearEcho()
        {
            _echoActive = false;
            _echoCountdown = -1f;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            // Continuous emission.
            if (_continuousActive)
            {
                _continuousTimer -= Time.deltaTime;
                if (_continuousTimer <= 0f)
                {
                    EmitShockwave();
                    _continuousTimer = _continuousInterval;
                }
            }

            // Echo: poll WardenBoss state once per frame to detect a transition into the Slam
            // sub-state. We cannot subscribe directly (no event exposed), but reading the
            // private _state enum via reflection is cheap and only runs while echo is enabled.
            if (_echoActive && _warden != null)
            {
                if (WardenBossStateReader.JustEnteredSlam(_warden))
                {
                    _echoCountdown = _echoDelay;
                }
            }
            if (_echoCountdown > 0f)
            {
                _echoCountdown -= Time.deltaTime;
                if (_echoCountdown <= 0f)
                {
                    EmitShockwave();
                    _echoCountdown = -1f;
                }
            }
        }

        private void EmitShockwave()
        {
            GameObject prefab = WardenBossStateReader.GetShockwavePrefab(_warden);
            if (prefab == null) return;

            Vector3 pos = transform.position;
            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(prefab, pos, Quaternion.identity)
                : Instantiate(prefab, pos, Quaternion.identity);
            if (instance == null) return;

            var wave = instance.GetComponent<EnemyShockwave>();
            if (wave != null)
            {
                float radius = (_enemy != null && _enemy.Data != null) ? _enemy.Data.aoeRadius : 0f;
                wave.Configure(radius, 0f, 0f); // 0s preserve the prefab's tuned damage/duration
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.LightShake();
            }
        }
    }

    /// <summary>
    /// Reads state from <see cref="WardenBoss"/> via reflection. Caches FieldInfo per process.
    /// All accessors degrade gracefully (return defaults, log once) so a future refactor of
    /// WardenBoss won't crash the phase system.
    /// </summary>
    internal static class WardenBossStateReader
    {
        private static FieldInfo _stateField;
        private static FieldInfo _shockwavePrefabField;
        private static bool _stateResolved, _prefabResolved;
        private static bool _stateMissingLogged, _prefabMissingLogged;

        // Per-instance tracking of the previous state value so we can detect transitions.
        private static readonly System.Collections.Generic.Dictionary<int, int> _lastStateByInstance = new();

        public static bool JustEnteredSlam(WardenBoss warden)
        {
            if (warden == null) return false;
            ResolveStateField();
            if (_stateField == null) return false;

            int current = (int)_stateField.GetValue(warden);
            int instanceId = warden.GetInstanceID();
            _lastStateByInstance.TryGetValue(instanceId, out int previous);
            _lastStateByInstance[instanceId] = current;

            // WardenBoss.State enum order is: Moving=0, Windup=1, Slam=2, Recover=3.
            // The Slam state is a single-frame transition; detecting the rising edge to it is
            // the cheapest "did a slam just happen?" signal we can build without modifying
            // the source file.
            const int Slam = 2;
            return current == Slam && previous != Slam;
        }

        public static GameObject GetShockwavePrefab(WardenBoss warden)
        {
            if (warden == null) return null;
            if (!_prefabResolved)
            {
                _shockwavePrefabField = typeof(WardenBoss).GetField(
                    "shockwavePrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _prefabResolved = true;
                if (_shockwavePrefabField == null && !_prefabMissingLogged)
                {
                    Debug.LogWarning(
                        "[PhaseShockwaveDriver] Could not reflect WardenBoss.shockwavePrefab — " +
                        "extra shockwaves from phase logic will be skipped. Either expose a public " +
                        "ShockwavePrefab getter on WardenBoss, or assign the prefab on a dedicated " +
                        "SerializeField on the PhaseShockwaveDriver (none exposed today).");
                    _prefabMissingLogged = true;
                }
            }
            return _shockwavePrefabField != null ? _shockwavePrefabField.GetValue(warden) as GameObject : null;
        }

        private static void ResolveStateField()
        {
            if (_stateResolved) return;
            _stateField = typeof(WardenBoss).GetField(
                "_state",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _stateResolved = true;
            if (_stateField == null && !_stateMissingLogged)
            {
                Debug.LogWarning(
                    "[PhaseShockwaveDriver] Could not reflect WardenBoss._state — echo mode (Phase 2 " +
                    "double-slam) will not trigger. Phase 3 enrage (continuous mode) is unaffected.");
                _stateMissingLogged = true;
            }
        }
    }
}
