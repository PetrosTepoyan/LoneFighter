using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.UI.WeaponHud
{
    /// <summary>
    /// Drives a short white-pulse "muzzle flash" on a slot when its bound weapon
    /// fires. Attached automatically by <see cref="WeaponSlotUi"/>; the parent slot
    /// calls <see cref="Pulse"/> after it detects a cooldown-timer reset (which is
    /// the only externally observable side-effect of <c>WeaponBase.TryFire()</c>).
    ///
    /// The component fades the target Image's color from white-hot back to its
    /// stored "rest" color over <see cref="duration"/> seconds using unscaled time
    /// so the flash still feels punchy at slow-mo / pause-edge frames.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponHudFlash : MonoBehaviour
    {
        [Tooltip("The Image that gets tinted. Defaults to this GameObject's Image if unset.")]
        [SerializeField] private Image target;

        [Tooltip("Total flash duration in seconds. 0.08 s feels snappy at 120 Hz.")]
        [SerializeField] private float duration = 0.08f;

        [Tooltip("Tint applied at the start of the pulse.")]
        [SerializeField] private Color flashColor = new(1f, 1f, 1f, 1f);

        private Color _restColor = Color.white;
        private Coroutine _running;

        private void Awake()
        {
            if (target == null) target = GetComponent<Image>();
            if (target != null) _restColor = target.color;
        }

        /// <summary>Captures the slot's "idle" tint so the pulse fades back to it.</summary>
        public void SetRestColor(Color c)
        {
            _restColor = c;
            if (_running == null && target != null) target.color = c;
        }

        /// <summary>Trigger the flash. Restarts an in-progress flash from its peak.</summary>
        public void Pulse()
        {
            if (target == null) return;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Linear fade: bright at k=0, rest at k=1.
                target.color = Color.Lerp(flashColor, _restColor, k);
                yield return null;
            }
            target.color = _restColor;
            _running = null;
        }
    }
}
