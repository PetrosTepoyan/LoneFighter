using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Weapons;

namespace LoneFighter.UI.WeaponHud
{
    /// <summary>
    /// One weapon slot in the HUD strip. Owns:
    /// <list type="bullet">
    /// <item>An icon <see cref="Image"/> (sprite resolved via <see cref="WeaponIconRegistry"/>).</item>
    /// <item>A radial-fill cooldown <see cref="Image"/> (Image.Type = Filled, Method = Radial360).</item>
    /// <item>A <see cref="WeaponLevelBadge"/> (bottom-right "+N").</item>
    /// <item>An optional <see cref="WeaponHudFlash"/> that pulses on every fire.</item>
    /// </list>
    ///
    /// <b>Cooldown source-of-truth</b> — <c>WeaponBase._cooldownTimer</c> is
    /// <c>protected</c>, so we can't read it through a normal API. The slot uses
    /// the helpers in <see cref="WeaponBaseExtensions"/>:
    /// <list type="number">
    /// <item>If reflection finds <c>_cooldownTimer</c> (default path), we compute
    ///       <c>fill = 1 - timer / effectiveCooldown</c> — accurate to one frame.</item>
    /// <item>If reflection fails (e.g. WeaponBase refactored), we fall back to a
    ///       local <c>_elapsedSinceLastFire</c> clock and use <c>data.cooldown</c>.
    ///       Less accurate against multipliers, but never misleading.</item>
    /// </list>
    /// In both cases, a "fire" is detected by watching the reflected timer for a
    /// sharp upward edge (timer was near zero last frame, jumps near full this
    /// frame). When that happens we kick the flash and reset the fallback clock.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponSlotUi : MonoBehaviour
    {
        [Header("Visual parts")]
        [Tooltip("Image whose sprite gets set to the weapon icon.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Image.Type = Filled, Fill Method = Radial 360, anchored over the icon.\n" +
                 "fillAmount goes 0 → 1 as the cooldown progresses, hits 1 right before fire.")]
        [SerializeField] private Image cooldownFill;

        [Tooltip("Bottom-right '+N' badge.")]
        [SerializeField] private WeaponLevelBadge levelBadge;

        [Tooltip("Optional white-pulse flash on fire. Auto-created on Awake if null.")]
        [SerializeField] private WeaponHudFlash flash;

        [Header("Tuning")]
        [Tooltip("If true, the cooldown ring is shown 'full' (1.0) when the weapon is ready, " +
                 "and drains from 1 → 0 during cooldown. If false, it FILLS 0 → 1.")]
        [SerializeField] private bool drainStyle = false;

        [Tooltip("Cooldown-timer threshold for detecting a fresh fire (a low → high jump).")]
        [SerializeField] private float fireDetectThreshold = 0.05f;

        private WeaponBase _weapon;
        private float _lastTimerSample;
        private float _elapsedFallback;     // Used only when reflection can't read the timer.

        public WeaponBase Weapon => _weapon;

        private void Awake()
        {
            if (flash == null) flash = GetComponentInChildren<WeaponHudFlash>();
            if (iconImage != null && flash != null) flash.SetRestColor(iconImage.color);
        }

        /// <summary>
        /// Bind this slot to a weapon and apply its icon. Call again to re-target.
        /// Safe to call with <c>null</c> to clear the slot.
        /// </summary>
        public void Bind(WeaponBase weapon, WeaponIconRegistry registry)
        {
            _weapon = weapon;
            _elapsedFallback = 0f;
            _lastTimerSample = 0f;

            if (iconImage != null)
            {
                Sprite icon = registry != null ? registry.Get(weapon != null ? weapon.Data : null) : null;
                if (icon != null) iconImage.sprite = icon;
                iconImage.enabled = (iconImage.sprite != null);
            }

            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = drainStyle ? 1f : 0f;
            }

            if (levelBadge != null) levelBadge.Bind(weapon);
            if (gameObject.name == "WeaponSlotUi" && weapon != null && weapon.Data != null)
            {
                gameObject.name = "WeaponSlot_" + weapon.Data.displayName;
            }
        }

        private void Update()
        {
            if (_weapon == null) return;

            float effectiveCooldown = _weapon.GetEffectiveCooldown();
            float timer = _weapon.TryGetCooldownTimer();
            float progress; // 0 = just fired, 1 = ready to fire.

            if (!float.IsNaN(timer))
            {
                // Detect a fire: timer was at/near zero last frame, now jumped near the full cooldown.
                if (_lastTimerSample <= fireDetectThreshold
                    && timer >= effectiveCooldown - fireDetectThreshold
                    && timer > _lastTimerSample + 0.01f)
                {
                    OnFireDetected();
                }
                _lastTimerSample = timer;

                // timer counts DOWN from effectiveCooldown → 0, so progress = 1 - timer/cd.
                progress = 1f - Mathf.Clamp01(timer / Mathf.Max(0.0001f, effectiveCooldown));
            }
            else
            {
                // Fallback: local clock, less accurate (ignores _cooldownMultiplier).
                _elapsedFallback += Time.deltaTime;
                float cd = Mathf.Max(0.01f, _weapon.Data != null ? _weapon.Data.cooldown : 0.5f);
                if (_elapsedFallback >= cd)
                {
                    OnFireDetected();
                    _elapsedFallback = 0f;
                }
                progress = Mathf.Clamp01(_elapsedFallback / cd);
            }

            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = drainStyle ? (1f - progress) : progress;
            }

            // Cheap to refresh — only updates the badge label when the integer tier changes.
            if (levelBadge != null) levelBadge.Refresh(_weapon);
        }

        private void OnFireDetected()
        {
            if (flash != null) flash.Pulse();
        }

        /// <summary>
        /// Called by <see cref="WeaponHudController"/> when the slot is being torn down
        /// (weapon list changed). Releases the weapon reference so subsequent Update()s
        /// do nothing.
        /// </summary>
        public void Unbind()
        {
            _weapon = null;
            if (cooldownFill != null) cooldownFill.fillAmount = drainStyle ? 1f : 0f;
        }
    }
}
