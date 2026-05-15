using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Effects.PlayerAura
{
    /// <summary>
    /// Renders a red, sin-wave-pulsed ring around the player whenever the player's
    /// HP fraction drops below <see cref="lowHpFraction"/> (default 30%).
    ///
    /// The pulse frequency (and amplitude) ramps up as HP gets lower, so the
    /// player feels increasingly stressed at sub-15% HP than at 28% HP.
    /// </summary>
    public class LowHealthAura : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("PlayerHealth to subscribe to. Resolved from this GameObject / parents if null.")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Sprite")]
        [Tooltip("Ring sprite (authored as a hollow circle). Required.")]
        [SerializeField] private Sprite ringSprite;

        [ColorUsage(true, true)]
        [SerializeField] private Color ringColor = new Color(1f, 0.1f, 0.1f, 1f);

        [SerializeField] private string sortingLayerName = "";
        [SerializeField] private int sortingOrder = -8;

        [Header("Threshold")]
        [Tooltip("HP fraction below which the aura activates.")]
        [SerializeField, Range(0.05f, 0.95f)] private float lowHpFraction = 0.3f;

        [Header("Pulse")]
        [Tooltip("Pulse frequency in Hz at the threshold (e.g. 30% HP).")]
        [SerializeField, Min(0.1f)] private float minPulseHz = 1.2f;

        [Tooltip("Pulse frequency in Hz at 0% HP (interpolated linearly from minPulseHz as HP drops).")]
        [SerializeField, Min(0.1f)] private float maxPulseHz = 3.5f;

        [Tooltip("World-space base scale of the aura ring.")]
        [SerializeField, Min(0.01f)] private float baseScale = 1.6f;

        [Tooltip("Additional scale amplitude added during the breathing pulse.")]
        [SerializeField, Range(0f, 1f)] private float scaleAmplitude = 0.18f;

        [Tooltip("Minimum alpha at the trough of the pulse.")]
        [SerializeField, Range(0f, 4f)] private float minAlpha = 0.25f;

        [Tooltip("Maximum alpha at the crest of the pulse.")]
        [SerializeField, Range(0f, 4f)] private float maxAlpha = 1f;

        private Transform _root;
        private SpriteRenderer _sr;
        private float _hpFraction = 1f;
        private bool _active;
        // Phase accumulator so we can change frequency without snapping.
        private float _phase;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
                _hpFraction = playerHealth.Max > 0f ? playerHealth.Current / playerHealth.Max : 1f;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
            if (_sr != null) _sr.enabled = false;
            _active = false;
        }

        private void HandleHealthChanged(float current, float max)
        {
            _hpFraction = max > 0f ? Mathf.Clamp01(current / max) : 1f;
        }

        private void EnsureRing()
        {
            if (_root != null) return;
            if (ringSprite == null) return;

            var go = new GameObject("LowHealthAura");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _root = go.transform;

            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = ringSprite;
            _sr.color = WithAlpha(ringColor, 0f);
            if (!string.IsNullOrEmpty(sortingLayerName)) _sr.sortingLayerName = sortingLayerName;
            _sr.sortingOrder = sortingOrder;
            _sr.enabled = false;
        }

        private void Update()
        {
            bool shouldBeActive = _hpFraction > 0f && _hpFraction <= lowHpFraction;

            if (shouldBeActive)
            {
                EnsureRing();
                if (_sr == null) return;

                if (!_active)
                {
                    _active = true;
                    _sr.enabled = true;
                }

                // Map HP fraction in [0, lowHpFraction] -> stress in [1, 0]
                // (stress = 1 when dead, 0 when at threshold).
                float stress = 1f - Mathf.Clamp01(_hpFraction / Mathf.Max(0.0001f, lowHpFraction));
                float hz = Mathf.Lerp(minPulseHz, maxPulseHz, stress);

                // Integrate phase so the wave is continuous across frequency changes.
                _phase += Time.deltaTime * hz * Mathf.PI * 2f;
                if (_phase > Mathf.PI * 200f) _phase -= Mathf.PI * 200f; // prevent drift to large floats

                float wave01 = (Mathf.Sin(_phase) + 1f) * 0.5f; // 0..1
                float scale = baseScale * (1f + (wave01 - 0.5f) * 2f * scaleAmplitude);
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave01);
                // Higher stress also boosts overall alpha so it feels more urgent.
                alpha *= Mathf.Lerp(0.75f, 1.25f, stress);

                _root.localScale = Vector3.one * scale;
                _sr.color = WithAlpha(ringColor, alpha * ringColor.a);
            }
            else if (_active)
            {
                _active = false;
                if (_sr != null) _sr.enabled = false;
            }
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
