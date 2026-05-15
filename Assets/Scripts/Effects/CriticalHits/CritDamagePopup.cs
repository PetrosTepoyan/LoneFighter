using TMPro;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.CriticalHits
{
    /// <summary>
    /// JACKPOT-feel floating number for critical hits.
    ///
    /// Differences vs the standard <see cref="LoneFighter.UI.DamagePopup"/>:
    ///   - Larger base scale (CRIT_BASE_SCALE).
    ///   - Animated yellow -> red color gradient over the lifetime, sampled from
    ///     <see cref="critColor"/>. Picked so the brightest frame lands at the
    ///     scale peak for maximum pop.
    ///   - Appends a "CRIT!" suffix to the damage number.
    ///   - Scale animation pulses 0 -> 1.5 -> 1.2 with a bouncy ease-out tail
    ///     instead of the regular popup's monotonic ease-in scale curve.
    ///   - Runs on <see cref="Time.unscaledDeltaTime"/> so it stays readable
    ///     through the crit hit-stop window driven by
    ///     <see cref="CritFreezeFrame"/>.
    ///
    /// Pooled: when its lifetime expires it asks <see cref="PoolService"/> to
    /// recycle the GameObject. If no pool exists (e.g. running standalone in a
    /// test scene) it falls back to <see cref="Object.Destroy"/>.
    ///
    /// The prefab is built procedurally by the editor menu
    /// <c>LoneFighter -> FX -> Generate Crit Popup Prefab</c> (see
    /// <c>Editor/CritPopupPrefabGenerator.cs</c>).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class CritDamagePopup : MonoBehaviour
    {
        private const float CRIT_BASE_SCALE = 1.0f;

        [Header("Timing")]
        [Tooltip("Total time on screen before the popup releases itself. Uses unscaled time.")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.85f;

        [Header("Motion")]
        [Tooltip("Upward speed (world units / second). A crit floats slower than a normal popup to draw the eye.")]
        [SerializeField] private float riseSpeed = 1.1f;
        [Tooltip("Random horizontal drift range so stacked crits don't perfectly overlap.")]
        [SerializeField] private Vector2 horizontalDrift = new Vector2(-0.25f, 0.25f);

        [Header("Scale Pulse (0 -> 1.5 -> 1.2 with bounce)")]
        [Tooltip("Scale multiplier over normalized lifetime. Designed to overshoot then settle.")]
        [SerializeField] private AnimationCurve scaleCurve = BuildDefaultScaleCurve();

        [Header("Color")]
        [Tooltip("Yellow -> orange -> red gradient sampled over normalized lifetime.")]
        [SerializeField] private Gradient critColor = BuildDefaultCritGradient();
        [Tooltip("Alpha envelope. Stays at 1 for most of the life then fades.")]
        [SerializeField] private AnimationCurve alphaCurve = BuildDefaultAlphaCurve();

        [Header("Text")]
        [SerializeField] private string critSuffix = " CRIT!";

        private TMP_Text _text;
        private float _t;
        private Vector3 _velocity;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            _t = 0f;
            float drift = Random.Range(horizontalDrift.x, horizontalDrift.y);
            _velocity = new Vector3(drift, riseSpeed, 0f);
            // Start at zero so the OnEnable frame doesn't render at full size before Update ticks.
            transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// Initialize the popup with a damage amount. Call once per pool-get,
        /// immediately after positioning the GameObject.
        /// </summary>
        public void Show(float amount)
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            int rounded = Mathf.RoundToInt(amount);
            _text.text = rounded.ToString() + critSuffix;
            // Reset state in case OnEnable was skipped (e.g. activated externally).
            _t = 0f;
            transform.localScale = Vector3.zero;
            ApplyColor(0f);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            transform.position += _velocity * Time.unscaledDeltaTime;

            float normalized = Mathf.Clamp01(_t / Mathf.Max(0.0001f, lifetime));

            // Sample the pulse curve; multiplier already encodes 0 -> 1.5 -> 1.2.
            float pulse = scaleCurve.Evaluate(normalized);
            transform.localScale = Vector3.one * (CRIT_BASE_SCALE * pulse);

            ApplyColor(normalized);

            if (_t >= lifetime)
            {
                Release();
            }
        }

        private void ApplyColor(float normalized)
        {
            if (_text == null) return;
            var color = critColor.Evaluate(normalized);
            color.a *= alphaCurve.Evaluate(normalized);
            _text.color = color;
        }

        private void Release()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }

        // ------------------------------------------------------------------
        // Default curves / gradients. Defined as static builders so they are
        // serialized into the prefab via the editor generator AND remain
        // tunable from the Inspector after the fact.
        // ------------------------------------------------------------------

        /// <summary>
        /// Bouncy scale pulse:
        ///   t=0.00  -> 0.0  (spawn)
        ///   t=0.18  -> 1.5  (overshoot peak)
        ///   t=0.32  -> 1.15 (rebound dip)
        ///   t=0.45  -> 1.25 (small second bounce)
        ///   t=1.00  -> 1.20 (settle)
        /// Tangents tuned so the entry into the peak is fast and the rebound is springy.
        /// </summary>
        private static AnimationCurve BuildDefaultScaleCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0.00f, 0.00f, 0f, 10f),
                new Keyframe(0.18f, 1.50f, 0f, 0f),
                new Keyframe(0.32f, 1.15f, 0f, 0f),
                new Keyframe(0.45f, 1.25f, 0f, 0f),
                new Keyframe(1.00f, 1.20f, 0f, 0f)
            );
            return curve;
        }

        private static Gradient BuildDefaultCritGradient()
        {
            var g = new Gradient { mode = GradientMode.Blend };
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.00f, 0.95f, 0.30f), 0.00f), // yellow
                    new GradientColorKey(new Color(1.00f, 0.65f, 0.10f), 0.45f), // orange
                    new GradientColorKey(new Color(1.00f, 0.20f, 0.10f), 1.00f), // red
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            return g;
        }

        private static AnimationCurve BuildDefaultAlphaCurve()
        {
            return new AnimationCurve(
                new Keyframe(0.00f, 1f),
                new Keyframe(0.70f, 1f),
                new Keyframe(1.00f, 0f));
        }
    }
}
