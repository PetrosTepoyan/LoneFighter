using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.UI.Minimap
{
    /// <summary>
    /// Tints the radar background red and adds a subtle alpha pulse when the local
    /// enemy density exceeds <see cref="dangerThreshold"/>. Eases back to the safe
    /// color when the crowd thins out, so the radar feels like it's "breathing".
    ///
    /// The controller pushes the current "enemies within radius" count each refresh
    /// via <see cref="ReportEnemyCount"/>; we do not query the registry ourselves to
    /// keep this component independent of which radius/filter the controller uses.
    /// </summary>
    [DisallowMultipleComponent]
    public class DangerProximity : MonoBehaviour
    {
        [Tooltip("Background image whose color is driven by danger level. If null, looks for an Image on this GameObject.")]
        [SerializeField] private Image background;

        [Tooltip("Color at 0% danger.")]
        [SerializeField] private Color safeColor = new Color(0.05f, 0.08f, 0.12f, 0.78f);

        [Tooltip("Color at 100% danger.")]
        [SerializeField] private Color dangerColor = new Color(0.45f, 0.06f, 0.08f, 0.92f);

        [Tooltip("Number of enemies (within radar radius) at which we are at full red.")]
        [SerializeField] private int dangerThreshold = 30;

        [Tooltip("Number of enemies (within radar radius) at which the tint starts to shift away from safe.")]
        [SerializeField] private int safeBelow = 6;

        [Tooltip("Lerp speed for the color transition. Higher = snappier.")]
        [SerializeField] private float lerpSpeed = 4f;

        [Tooltip("How much the alpha pulses while in danger (0 disables pulse).")]
        [Range(0f, 0.3f)] [SerializeField] private float pulseAmplitude = 0.08f;

        [Tooltip("Pulse frequency in Hz while in danger.")]
        [SerializeField] private float pulseFrequency = 2f;

        [Tooltip("Use unscaled time so the danger pulse keeps animating while paused.")]
        [SerializeField] private bool useUnscaledTime = true;

        private float _smoothedDanger;
        private float _lastReportedCount;

        public bool IsInDanger => _smoothedDanger >= 0.5f;
        public float DangerLevel01 => _smoothedDanger;

        private void Awake()
        {
            if (background == null) background = GetComponent<Image>();
        }

        /// <summary>
        /// Called by <see cref="MinimapController"/> each radar refresh with the number of
        /// enemies currently inside the radar's <c>worldRadius</c>.
        /// </summary>
        public void ReportEnemyCount(int enemyCount)
        {
            _lastReportedCount = enemyCount;
        }

        private void Update()
        {
            // Map count -> [0..1] danger level, with a safe deadzone below safeBelow.
            float span = Mathf.Max(1, dangerThreshold - safeBelow);
            float target = Mathf.Clamp01((_lastReportedCount - safeBelow) / span);

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _smoothedDanger = Mathf.MoveTowards(
                _smoothedDanger,
                target,
                lerpSpeed * dt);

            if (background == null) return;

            // Subtle pulse only while we're meaningfully in danger so the safe state stays calm.
            float pulse = 0f;
            if (_smoothedDanger > 0.05f && pulseAmplitude > 0f)
            {
                float t = useUnscaledTime ? Time.unscaledTime : Time.time;
                // Sine in [-1..1] -> multiply by current danger so a near-safe state barely pulses.
                pulse = Mathf.Sin(t * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude * _smoothedDanger;
            }

            var c = Color.Lerp(safeColor, dangerColor, _smoothedDanger);
            c.a = Mathf.Clamp01(c.a + pulse);
            background.color = c;
        }

        public void SetBackground(Image image)
        {
            background = image;
        }

        public void SetThreshold(int threshold, int safeBelowCount)
        {
            dangerThreshold = Mathf.Max(1, threshold);
            safeBelow = Mathf.Clamp(safeBelowCount, 0, dangerThreshold - 1);
        }
    }
}
