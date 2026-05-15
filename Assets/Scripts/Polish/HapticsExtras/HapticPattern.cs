using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Polish.HapticsExtras
{
    /// <summary>
    /// An ordered sequence of haptic pulses with gaps between them. Each step is an
    /// (intensity, duration, gapMs) tuple. The existing <see cref="HapticsService"/>
    /// owns pulse-shape (gamepad / mobile fallback); this layer only schedules WHEN
    /// pulses fire and WHICH intensity is used.
    ///
    /// <para><b>Step semantics</b></para>
    /// <list type="bullet">
    ///   <item><description><c>intensity</c>: forwarded to <see cref="HapticsService.Pulse"/>.</description></item>
    ///   <item><description><c>duration</c>: how long we "hold" this step before moving on.
    ///       Most platforms (Android <see cref="UnityEngine.Handheld.Vibrate"/>) ignore duration
    ///       because the OS picks the pulse length; we still wait the requested time so subsequent
    ///       steps don't stack on top of each other.</description></item>
    ///   <item><description><c>gapMs</c>: extra silent delay AFTER the step's duration before
    ///       the next step starts. Use this to space out triple-tap style patterns.</description></item>
    /// </list>
    ///
    /// All waits use unscaled time so patterns play correctly during hit-stop / level-up freeze
    /// (when <see cref="Time.timeScale"/> is 0).
    /// </summary>
    public class HapticPattern
    {
        /// <summary>One step in the pattern.</summary>
        public readonly struct Step
        {
            public readonly HapticIntensity Intensity;
            /// <summary>Hold time in seconds before the next step is considered (clamped to ≥ 0).</summary>
            public readonly float Duration;
            /// <summary>Silent gap after the step's hold time, in milliseconds (clamped to ≥ 0).</summary>
            public readonly float GapMs;

            public Step(HapticIntensity intensity, float duration, float gapMs)
            {
                Intensity = intensity;
                Duration = Mathf.Max(0f, duration);
                GapMs = Mathf.Max(0f, gapMs);
            }
        }

        /// <summary>Human-readable name used for logging / debugging.</summary>
        public string Name { get; }

        /// <summary>The ordered list of steps.</summary>
        public IReadOnlyList<Step> Steps => _steps;

        private readonly List<Step> _steps;

        public HapticPattern(string name, IEnumerable<Step> steps)
        {
            Name = string.IsNullOrEmpty(name) ? "Pattern" : name;
            _steps = steps != null ? new List<Step>(steps) : new List<Step>();
        }

        /// <summary>Fluent builder that creates a pattern from a sequence of steps.</summary>
        public static HapticPattern Build(string name, params Step[] steps)
        {
            return new HapticPattern(name, steps);
        }

        /// <summary>Convenience factory that takes (intensity, durationSeconds, gapMs) tuples.</summary>
        public static HapticPattern FromTuples(string name, params (HapticIntensity intensity, float duration, float gapMs)[] steps)
        {
            var list = new List<Step>(steps != null ? steps.Length : 0);
            if (steps != null)
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    list.Add(new Step(steps[i].intensity, steps[i].duration, steps[i].gapMs));
                }
            }
            return new HapticPattern(name, list);
        }

        /// <summary>
        /// Coroutine that drives the pattern through the given <see cref="HapticsService"/>.
        /// Safe to interrupt with <see cref="MonoBehaviour.StopCoroutine"/>; remaining steps
        /// simply won't fire.
        /// </summary>
        /// <param name="svc">Target haptics service. If <c>null</c>, the coroutine yields silently
        /// so callers don't need a null-check around <c>StartCoroutine</c>.</param>
        /// <param name="intensityScalarOverride">Optional 0..1 master scalar — if &lt; 1 some
        /// individual steps may be downgraded (Heavy→Medium, Medium→Light) to dial back chaos.
        /// Default of 1.0 plays the pattern as authored.</param>
        public IEnumerator Play(HapticsService svc, float intensityScalarOverride = 1f)
        {
            if (_steps == null || _steps.Count == 0) yield break;

            float scalar = Mathf.Clamp01(intensityScalarOverride);

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];

                if (svc != null)
                {
                    var pulseIntensity = ScaleIntensity(step.Intensity, scalar);
                    svc.Pulse(pulseIntensity);
                }

                if (step.Duration > 0f)
                {
                    yield return new WaitForSecondsRealtime(step.Duration);
                }

                if (step.GapMs > 0f)
                {
                    yield return new WaitForSecondsRealtime(step.GapMs / 1000f);
                }
            }
        }

        /// <summary>Downgrade an intensity step based on a 0..1 scalar. Cheap, predictable.</summary>
        private static HapticIntensity ScaleIntensity(HapticIntensity src, float scalar)
        {
            if (scalar >= 0.9f) return src;
            if (scalar <= 0.1f) return HapticIntensity.Light;

            // Two coarse buckets — keep it simple and readable.
            if (scalar < 0.5f)
            {
                // Aggressive downgrade.
                if (src == HapticIntensity.Heavy) return HapticIntensity.Light;
                if (src == HapticIntensity.Medium) return HapticIntensity.Light;
                return HapticIntensity.Light;
            }
            // Mild downgrade.
            if (src == HapticIntensity.Heavy) return HapticIntensity.Medium;
            return src;
        }
    }
}
