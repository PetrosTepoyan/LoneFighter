using UnityEngine;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Flashes the enemy's SpriteRenderer pure white for a brief moment on hit,
    /// then restores the original color. Mimics the "got hit" feedback common to
    /// 2D action games (Hollow Knight, Vampire Survivors, etc.).
    ///
    /// Attached to enemies automatically by <see cref="EnemyFeedbackAttacher"/>.
    /// Successive hits restart the timer — the flash stays at peak color while
    /// the player chains fast attacks, then fades back when they stop firing.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySpriteFlash : MonoBehaviour
    {
        [Tooltip("How long the white flash lasts in seconds (unscaled).")]
        [SerializeField, Min(0f)] private float duration = 0.06f;
        [Tooltip("Flash tint. Use pure white for the classic 'flash' look.")]
        [SerializeField] private Color flashColor = Color.white;

        private SpriteRenderer[] _renderers;
        private Color[] _originalColors;
        private float _flashRemaining;
        private bool _flashing;

        private void Awake()
        {
            ResolveRenderers();
        }

        private void OnEnable()
        {
            // The enemy may be pooled — re-capture colors in case the visual
            // child got swapped between uses.
            ResolveRenderers();
            _flashing = false;
            _flashRemaining = 0f;
        }

        private void OnDisable()
        {
            RestoreColors();
            _flashing = false;
            _flashRemaining = 0f;
        }

        private void ResolveRenderers()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _originalColors[i] = _renderers[i].color;
            }
        }

        /// <summary>
        /// Trigger the white flash. Idempotent — additional calls during the
        /// flash window extend it to the full duration again.
        /// </summary>
        public void Play()
        {
            if (_renderers == null || _renderers.Length == 0) ResolveRenderers();
            if (_renderers == null || _renderers.Length == 0) return;

            if (!_flashing)
            {
                ApplyFlashColor();
                _flashing = true;
            }
            _flashRemaining = duration;
        }

        private void Update()
        {
            if (!_flashing) return;
            _flashRemaining -= Time.unscaledDeltaTime;
            if (_flashRemaining <= 0f)
            {
                RestoreColors();
                _flashing = false;
            }
        }

        private void ApplyFlashColor()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                // Preserve original alpha so transparent-on-spawn effects aren't broken.
                float a = _renderers[i].color.a;
                var c = flashColor;
                c.a = Mathf.Min(a, flashColor.a) > 0f ? a : flashColor.a;
                _renderers[i].color = c;
            }
        }

        private void RestoreColors()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length && i < _originalColors.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].color = _originalColors[i];
            }
        }
    }
}
