using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.UI.BossIntro
{
    /// <summary>
    /// Tiny wrapper that plays a boss stinger <see cref="AudioClip"/> through
    /// <see cref="AudioManager.Instance"/> when one is available, falling back to a
    /// local <see cref="AudioSource"/> when the central audio manager is not in the
    /// scene (e.g. early in boot or inside the boss-intro test scene).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boss stingers want to cut through everything else playing: the wrapper hands
    /// the clip to <c>AudioManager.PlaySfx</c> with a deliberately loud
    /// <c>volumeScale</c> so it pops above the normal sfx mix. <c>PlaySfx</c>
    /// uses <c>PlayOneShot</c> internally so the existing sfx source is reused —
    /// no extra <c>AudioSource</c> is allocated on the audio manager.
    /// </para>
    /// <para>
    /// The local fallback <see cref="AudioSource"/> is added on-demand so this
    /// component is drop-in: no inspector wiring required for the audio path.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class BossStingerPlayer : MonoBehaviour
    {
        [Tooltip("Volume multiplier applied on top of the AudioManager's SFX bus. >1 boosts the stinger above normal sfx.")]
        [Range(0f, 4f)]
        [SerializeField] private float volumeScale = 1.6f;

        [Tooltip("Fallback AudioSource used only when AudioManager.Instance is null. Auto-created on demand.")]
        [SerializeField] private AudioSource fallbackSource;

        /// <summary>
        /// Play <paramref name="clip"/> immediately. No-op if the clip is null.
        /// </summary>
        public void Play(AudioClip clip)
        {
            if (clip == null) return;

            var mgr = AudioManager.Instance;
            if (mgr != null)
            {
                // High-priority / high-volume stinger. AudioManager clamps internally; values >1
                // are safe and produce the desired "above-the-mix" punch.
                mgr.PlaySfx(clip, Mathf.Max(0f, volumeScale));
                return;
            }

            // Fallback path — used only when AudioManager hasn't booted yet.
            if (fallbackSource == null)
            {
                fallbackSource = gameObject.AddComponent<AudioSource>();
                fallbackSource.playOnAwake = false;
                fallbackSource.loop = false;
                fallbackSource.spatialBlend = 0f; // 2D
                fallbackSource.priority = 0;      // highest priority
            }
            fallbackSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }
    }
}
