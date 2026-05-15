using System.Collections;
using UnityEngine;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Six-stage cinematic for "the boss is dying and the screen is about to be eaten":
    ///
    ///   Stage 1: SlowMo                  — Time.timeScale dips to slowMoScale for slowMoDuration.
    ///   Stage 2: Three Medium explosions — fired in sequence around the boss in a triangle.
    ///   Stage 3: Anticipation pause      — brief silence (still in slow-mo) before the punchline.
    ///   Stage 4: MEGA NUKE               — one centered Nuke detonation.
    ///   Stage 5: Screen white-out        — full-screen flash overlay punches white.
    ///   Stage 6: Resume                  — slow-mo lifted, control returned.
    ///
    /// Implemented as an unscaled-time coroutine: <see cref="WaitForSecondsRealtime"/>
    /// is used everywhere so the choreography runs at the wall-clock pace the designer
    /// authored, regardless of slow-mo or hit-stop modifying <c>Time.timeScale</c>.
    ///
    /// Use:
    ///   <code>BossDeathMegaExplosion.Play(boss.transform.position);</code>
    /// or drop the component into a scene and call <see cref="Begin"/> when scripted.
    /// </summary>
    public class BossDeathMegaExplosion : MonoBehaviour
    {
        [Header("Stage 1: SlowMo")]
        [SerializeField, Range(0.02f, 1f)] private float slowMoScale = 0.18f;
        [SerializeField, Min(0f)] private float slowMoDuration = 1.6f;

        [Header("Stage 2: Three Mediums")]
        [SerializeField, Min(0f)] private float secondsBetweenMediums = 0.22f;
        [SerializeField, Min(0f)] private float mediumOffsetRadius = 1.4f;
        [SerializeField, Min(0f)] private float mediumDamagePerExplosion = 0f;

        [Header("Stage 3: Anticipation")]
        [SerializeField, Min(0f)] private float pauseBeforeMegaSeconds = 0.45f;

        [Header("Stage 4: MEGA NUKE")]
        [SerializeField] private ExplosionTier finaleTier = ExplosionTier.Nuke;
        [SerializeField, Min(0f)] private float finaleDamage = 0f;

        [Header("Stage 5: Whiteout")]
        [SerializeField] private Color whiteoutColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float whiteoutAlpha = 0.95f;
        [SerializeField, Min(0f)] private float whiteoutDuration = 0.55f;

        [Header("Lifecycle")]
        [Tooltip("Destroy this host after the cinematic completes. Use on disposable hosts spawned by Play().")]
        [SerializeField] private bool destroyOnFinish = false;

        private Coroutine _routine;
        private bool _slowMoActive;
        private float _slowMoRestoreScale = 1f;

        /// <summary>
        /// Fire-and-forget convenience. Spawns a host GameObject at <paramref name="bossWorldPos"/>,
        /// runs the cinematic, and disposes everything when done.
        /// </summary>
        public static BossDeathMegaExplosion Play(Vector3 bossWorldPos)
        {
            var go = new GameObject("BossDeathMegaExplosion");
            go.transform.position = bossWorldPos;
            var c = go.AddComponent<BossDeathMegaExplosion>();
            c.destroyOnFinish = true;
            c.Begin(bossWorldPos);
            return c;
        }

        public void Begin(Vector3 worldPos)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(RunSequence(worldPos));
        }

        private void OnDisable()
        {
            // Make sure we never leave the game stuck in slow-mo if the host is destroyed
            // mid-sequence (e.g. scene transition during a death cinematic).
            if (_slowMoActive)
            {
                Time.timeScale = _slowMoRestoreScale;
                _slowMoActive = false;
            }
        }

        private IEnumerator RunSequence(Vector3 worldPos)
        {
            if (ExplosionService.Instance == null)
            {
                // Wait one frame in case the service is still spinning up.
                yield return null;
            }
            if (ExplosionService.Instance == null)
            {
                Debug.LogWarning("[BossDeathMegaExplosion] No ExplosionService.Instance — aborting cinematic.");
                if (destroyOnFinish) Destroy(gameObject);
                yield break;
            }

            // ===== Stage 1: SLOWMO =====
            _slowMoRestoreScale = Time.timeScale;
            Time.timeScale = slowMoScale;
            _slowMoActive = true;

            // ===== Stage 2: 3 MEDIUMS (triangular pattern around the boss) =====
            float startWait = slowMoDuration * 0.25f;
            if (startWait > 0f) yield return new WaitForSecondsRealtime(startWait);

            for (int i = 0; i < 3; i++)
            {
                float ang = (i * 120f + 30f) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * mediumOffsetRadius;
                ExplosionService.Instance.Detonate(worldPos + offset, ExplosionTier.Medium, mediumDamagePerExplosion);
                if (i < 2 && secondsBetweenMediums > 0f)
                    yield return new WaitForSecondsRealtime(secondsBetweenMediums);
            }

            // ===== Stage 3: PAUSE =====
            if (pauseBeforeMegaSeconds > 0f)
                yield return new WaitForSecondsRealtime(pauseBeforeMegaSeconds);

            // ===== Stage 4: MEGA NUKE =====
            ExplosionService.Instance.Detonate(worldPos, finaleTier, finaleDamage);

            // ===== Stage 5: WHITEOUT =====
            // The Nuke profile already produces a screen flash, but we layer an explicit
            // longer-duration overlay punch so the screen actually clears to white before
            // fading down.
            var overlay = ScreenFlashOverlay.Instance != null
                ? ScreenFlashOverlay.Instance
                : ScreenFlashOverlay.GetOrCreate();
            if (overlay != null)
            {
                var col = whiteoutColor;
                col.a = whiteoutAlpha;
                overlay.Flash(col, whiteoutDuration);
            }

            // Hold the slow-mo through ~70% of the whiteout, then release.
            float holdTime = Mathf.Max(0f, whiteoutDuration * 0.7f);
            if (holdTime > 0f) yield return new WaitForSecondsRealtime(holdTime);

            // ===== Stage 6: RESUME =====
            Time.timeScale = _slowMoRestoreScale;
            _slowMoActive = false;

            // Let the rest of the whiteout fade naturally.
            float tail = Mathf.Max(0f, whiteoutDuration * 0.3f);
            if (tail > 0f) yield return new WaitForSecondsRealtime(tail);

            _routine = null;
            if (destroyOnFinish) Destroy(gameObject);
        }
    }
}
