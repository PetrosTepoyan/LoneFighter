using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Singleton facade. The one entry point any combat code can call to dispatch
    /// "the player should FEEL this" feedback. Every hit fans out into multiple
    /// stacking channels (sprite flash, knockback, hit-pause, ripple, popup,
    /// vignette, freeze-frame, blood overlay, low-HP warp, etc.).
    ///
    /// All channels are optional/best-effort: if the corresponding component or
    /// scene reference is missing we silently no-op so combat scripts never need
    /// to null-check before calling in.
    ///
    /// You can either:
    ///   1. Drop a single GameObject with this component in your scene; the rest
    ///      of the feedback components self-register via FindFirstObjectByType
    ///      when needed.
    ///   2. Or attach the full kit (HitFeedbackService + PlayerHitVignette +
    ///      PlayerHitFreezeFrame + BloodSplashOverlay + LowHpScreenWarp +
    ///      HitDamageNumbers + EnemyFeedbackAttacher) to a single
    ///      "_HitFeedback" GameObject and assign the references in inspector.
    /// </summary>
    public class HitFeedbackService : MonoBehaviour
    {
        public static HitFeedbackService Instance { get; private set; }

        [Header("Channels (optional — auto-resolved at runtime if blank)")]
        [SerializeField] private PlayerHitVignette playerVignette;
        [SerializeField] private PlayerHitFreezeFrame playerFreezeFrame;
        [SerializeField] private BloodSplashOverlay bloodSplash;
        [SerializeField] private LowHpScreenWarp lowHpWarp;
        [SerializeField] private HitDamageNumbers damageNumbers;
        [SerializeField] private ImpactRipple impactRipple;

        [Header("Tuning")]
        [Tooltip("Damage popups are dispatched for every hit. Set false to disable.")]
        [SerializeField] private bool emitDamageNumbers = true;
        [Tooltip("If true, spawn the impact ripple shockwave sprite on every hit.")]
        [SerializeField] private bool emitImpactRipples = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Lazy-resolve any unset references against the active scene.
            if (playerVignette == null) playerVignette = FindFirstObjectByType<PlayerHitVignette>();
            if (playerFreezeFrame == null) playerFreezeFrame = FindFirstObjectByType<PlayerHitFreezeFrame>();
            if (bloodSplash == null) bloodSplash = FindFirstObjectByType<BloodSplashOverlay>();
            if (lowHpWarp == null) lowHpWarp = FindFirstObjectByType<LowHpScreenWarp>();
            if (damageNumbers == null) damageNumbers = FindFirstObjectByType<HitDamageNumbers>();
            if (impactRipple == null) impactRipple = FindFirstObjectByType<ImpactRipple>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Fire all enemy-side feedback channels for a hit on the given enemy.
        /// </summary>
        /// <param name="enemy">The hit enemy. May be null — channels self-skip.</param>
        /// <param name="damage">Damage applied this frame. Used for popup size.</param>
        /// <param name="hitDirection">Direction the damage came from (unit vector,
        /// from attacker toward enemy). Used for knockback and popup drift.</param>
        public void OnEnemyHit(EnemyBase enemy, float damage, Vector2 hitDirection)
        {
            if (enemy == null) return;
            Vector3 worldPos = enemy.transform.position;

            // Per-enemy components attached by EnemyFeedbackAttacher.
            var flash = enemy.GetComponent<EnemySpriteFlash>();
            if (flash != null) flash.Play();

            var knockback = enemy.GetComponent<EnemyKnockback>();
            if (knockback != null) knockback.Apply(hitDirection);

            var hitPause = enemy.GetComponent<EnemyHitPause>();
            if (hitPause != null) hitPause.Apply();

            // Global feedback.
            if (emitImpactRipples && impactRipple != null)
            {
                impactRipple.Spawn(worldPos);
            }
            if (emitDamageNumbers && damageNumbers != null)
            {
                damageNumbers.Show(worldPos, damage, hitDirection);
            }
        }

        /// <summary>
        /// Fire all player-side feedback channels for a hit on the player.
        /// </summary>
        /// <param name="damage">Damage applied this frame.</param>
        /// <param name="hitDirection">Direction from attacker toward player.</param>
        public void OnPlayerHit(float damage, Vector2 hitDirection)
        {
            if (playerVignette != null) playerVignette.Pulse();
            if (playerFreezeFrame != null) playerFreezeFrame.NotifyDamage(damage);
            if (bloodSplash != null) bloodSplash.Refresh();
            if (lowHpWarp != null) lowHpWarp.Refresh();

            // Show a popup at the player's feet too, biased into hitDirection.
            if (emitDamageNumbers && damageNumbers != null)
            {
                var player = LoneFighter.Player.PlayerController.Instance;
                if (player != null)
                {
                    damageNumbers.Show(player.transform.position, damage, hitDirection, isPlayer: true);
                }
            }

            if (emitImpactRipples && impactRipple != null)
            {
                var player = LoneFighter.Player.PlayerController.Instance;
                if (player != null) impactRipple.Spawn(player.transform.position);
            }
        }
    }
}
