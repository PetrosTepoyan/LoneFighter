using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.CriticalHits
{
    /// <summary>
    /// Tiny static helper that triggers a micro hit-stop on a crit.
    ///
    /// Delegates to <see cref="FxService.HitStop"/> if an FxService singleton
    /// exists in the scene (the project's central hit-stop manager already
    /// preserves and restores the prior <see cref="Time.timeScale"/>).
    /// If FxService isn't present we fall back to a self-managed
    /// MonoBehaviour-driven freeze so crits still feel impactful in test
    /// scenes that don't bootstrap the full systems layer.
    ///
    /// Default duration: 0.04s (40 ms) — short enough that 120Hz fire-rate
    /// builds still feel responsive, long enough to register the punch.
    /// </summary>
    public static class CritFreezeFrame
    {
        /// <summary>Project default per design spec.</summary>
        public const float DEFAULT_DURATION = 0.04f;

        /// <summary>
        /// Apply a brief hit-stop. Caller controls duration so different crit
        /// tiers (lucky/mega/etc.) can dial it independently.
        /// </summary>
        /// <param name="seconds">Duration in unscaled seconds. Clamped to >= 0.</param>
        public static void Apply(float seconds = DEFAULT_DURATION)
        {
            if (seconds <= 0f) return;

            if (FxService.Instance != null)
            {
                FxService.Instance.HitStop(seconds);
                return;
            }

            Fallback.Run(seconds);
        }

        /// <summary>
        /// Standalone hit-stop driver used when <see cref="FxService"/> is
        /// missing. Self-instantiates a hidden runner GameObject the first
        /// time it's needed. Restores the captured time-scale even if Apply
        /// is called again during the freeze window (longest duration wins).
        /// </summary>
        private class Fallback : MonoBehaviour
        {
            private static Fallback _instance;
            private float _remaining;
            private float _originalTimeScale = 1f;

            public static void Run(float seconds)
            {
                if (_instance == null)
                {
                    var go = new GameObject("CritFreezeFrame.Fallback");
                    DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<Fallback>();
                }
                _instance.Begin(seconds);
            }

            private void Begin(float seconds)
            {
                if (_remaining <= 0f)
                {
                    _originalTimeScale = Time.timeScale;
                }
                _remaining = Mathf.Max(_remaining, seconds);
                Time.timeScale = 0f;
            }

            private void Update()
            {
                if (_remaining <= 0f) return;
                _remaining -= Time.unscaledDeltaTime;
                if (_remaining <= 0f)
                {
                    Time.timeScale = _originalTimeScale;
                }
            }
        }
    }
}
