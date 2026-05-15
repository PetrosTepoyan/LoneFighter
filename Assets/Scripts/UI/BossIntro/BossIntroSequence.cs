using System.Collections;
using UnityEngine;

namespace LoneFighter.UI.BossIntro
{
    /// <summary>
    /// Coroutine body that orchestrates the cinematic boss intro:
    /// <list type="number">
    ///   <item>Slow down the game to <c>0.2x</c> for ~1.2 seconds of unscaled time.</item>
    ///   <item>Slide the <see cref="BossNameBanner"/> in from the screen edges.</item>
    ///   <item>Darken everything except the boss with a <see cref="BossSpotlight"/>.</item>
    ///   <item>Play the stinger via <see cref="BossStingerPlayer"/>.</item>
    ///   <item>Restore the original <c>Time.timeScale</c> and slide the banner out
    ///         over <c>0.4s</c>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every wait uses <see cref="WaitForSecondsRealtime"/> so the choreography is
    /// independent of the game's current time scale (which this very sequence is
    /// manipulating).
    /// </para>
    /// <para>
    /// The original <c>Time.timeScale</c> is captured up-front and restored at the
    /// end with the same easing ramp, so the player feels a "slow then snap back"
    /// instead of a jarring teleport. If something else has changed the timescale
    /// during the cinematic (e.g. the player paused), the restore step still
    /// clamps to the value we originally captured — we deliberately do not race
    /// with other timescale owners.
    /// </para>
    /// </remarks>
    public class BossIntroSequence
    {
        private readonly BossNameBanner _banner;
        private readonly BossSpotlight _spotlight;
        private readonly BossStingerPlayer _stinger;

        // Tunable durations — kept here rather than the controller so the sequence is
        // self-contained and easy to unit-tweak without re-serialising the controller.
        private const float SlowMoTimeScale = 0.2f;
        private const float SlowMoDuration  = 1.2f;
        private const float BannerInDuration  = 0.45f;
        private const float BannerHoldDuration = 1.4f;
        private const float BannerOutDuration  = 0.4f;
        private const float TimeRampDuration   = 0.35f;

        public BossIntroSequence(BossNameBanner banner, BossSpotlight spotlight, BossStingerPlayer stinger)
        {
            _banner = banner;
            _spotlight = spotlight;
            _stinger = stinger;
        }

        public IEnumerator Run(BossMeta meta, Transform target)
        {
            if (meta == null) yield break;

            float originalScale = Time.timeScale;

            // ---- 1. Ramp into slow-mo ----
            yield return RampTimeScale(originalScale, SlowMoTimeScale, TimeRampDuration);

            // ---- 2. Show banner + spotlight, fire stinger ----
            if (_spotlight != null) _spotlight.Show(meta, target);
            if (_banner != null) _banner.SlideIn(meta, BannerInDuration);
            if (_stinger != null && meta.stinger != null) _stinger.Play(meta.stinger);

            // Hold the slow-mo while the banner is on screen.
            yield return new WaitForSecondsRealtime(SlowMoDuration);

            // ---- 3. Restore time scale ----
            yield return RampTimeScale(SlowMoTimeScale, originalScale, TimeRampDuration);

            // ---- 4. Linger so the player can read the subtitle at full speed, then dismiss ----
            float remaining = Mathf.Max(0f, BannerHoldDuration - SlowMoDuration);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

            // ---- 5. Slide banner + spotlight back out ----
            if (_banner != null) _banner.SlideOut(BannerOutDuration);
            if (_spotlight != null) _spotlight.Hide(BannerOutDuration);
            yield return new WaitForSecondsRealtime(BannerOutDuration);
        }

        private static IEnumerator RampTimeScale(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                Time.timeScale = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Smoothstep-style ease for a natural slow-in / slow-out.
                float eased = u * u * (3f - 2f * u);
                Time.timeScale = Mathf.Lerp(from, to, eased);
                yield return null;
            }
            Time.timeScale = to;
        }
    }
}
