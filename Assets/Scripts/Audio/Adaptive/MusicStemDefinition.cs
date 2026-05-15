using UnityEngine;

namespace LoneFighter.Audio.Adaptive
{
    /// <summary>
    /// A single stem inside an <see cref="AdaptiveMusicProfile"/>. Each stem is its own
    /// audio layer (bass / drums / lead / pads etc.) that fades in and out based on the
    /// current combat intensity reported by <see cref="IntensityResolver"/>.
    ///
    /// All stems in a profile are expected to share the same length / BPM / sample rate
    /// so that they can play synchronously from t=0 and remain in phase. The threshold
    /// model is "on/off with a fade": the stem fades to its full target volume whenever
    /// the live intensity is at or above <see cref="intensityThreshold"/>, and fades back
    /// to silence when it drops below.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MusicStem",
        menuName = "LoneFighter/Audio/Adaptive/Music Stem",
        order = 200)]
    public class MusicStemDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier for this stem (e.g. 'bass', 'drums', 'lead', 'pads'). " +
                 "Used by the controller as the AudioSource GameObject name and by editor previews.")]
        public string id = "bass";

        [Tooltip("Looping audio clip for this stem. Must match the BPM and length of every " +
                 "other stem in the profile or the layers will drift out of sync.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        [Tooltip("Intensity (0..1) at which this stem becomes audible. Above this value the " +
                 "stem fades up to its target volume; below it the stem fades to zero.")]
        public float intensityThreshold = 0f;

        [Min(0f)]
        [Tooltip("Seconds taken to fade the stem from silent to full when intensity rises " +
                 "above the threshold.")]
        public float fadeInSeconds = 1.5f;

        [Min(0f)]
        [Tooltip("Seconds taken to fade the stem from full to silent when intensity drops " +
                 "below the threshold.")]
        public float fadeOutSeconds = 2.0f;

        [Range(0f, 1f)]
        [Tooltip("Per-stem volume cap (0..1). Multiplied with the controller's master volume.")]
        public float targetVolume = 1f;
    }
}
