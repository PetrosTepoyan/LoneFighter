using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Audio.Adaptive
{
    /// <summary>
    /// A multi-stem adaptive music track. Every <see cref="MusicStemDefinition"/> in
    /// <see cref="stems"/> will be instantiated as its own looping AudioSource by
    /// <see cref="AdaptiveMusicController"/>, all started simultaneously so they remain
    /// phase-locked.
    ///
    /// IMPORTANT: every clip referenced by <see cref="stems"/> must be authored at the
    /// same <see cref="bpm"/> and <see cref="lengthSeconds"/>. The controller does NOT
    /// resample or time-stretch — it relies on the source content being aligned.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AdaptiveMusicProfile",
        menuName = "LoneFighter/Audio/Adaptive/Music Profile",
        order = 201)]
    public class AdaptiveMusicProfile : ScriptableObject
    {
        [Tooltip("Display name shown in the editor window.")]
        public string trackName = "Untitled Adaptive Track";

        [Min(1f)]
        [Tooltip("Authored BPM — informational, used for loop alignment hints in the editor. " +
                 "All stems must share this BPM.")]
        public float bpm = 120f;

        [Min(0.01f)]
        [Tooltip("Authored loop length in seconds. All stems must be exactly this long.")]
        public float lengthSeconds = 32f;

        [Range(0f, 1f)]
        [Tooltip("Master volume applied to every stem before per-stem volumes.")]
        public float masterVolume = 1f;

        [Tooltip("The ordered list of stems. Order is purely cosmetic for editor display; " +
                 "playback order is irrelevant because every stem starts at the same instant.")]
        public List<MusicStemDefinition> stems = new List<MusicStemDefinition>();

        /// <summary>
        /// Returns true if every stem has a clip that matches the profile's authored length
        /// (within ~10 ms tolerance). Returns the first failing stem id in <paramref name="firstMismatch"/>.
        /// Used by the editor window's validation pass; cheap enough to call at runtime too.
        /// </summary>
        public bool ValidateStemAlignment(out string firstMismatch)
        {
            firstMismatch = null;
            if (stems == null) return true;
            for (int i = 0; i < stems.Count; i++)
            {
                var s = stems[i];
                if (s == null) continue;
                if (s.clip == null) continue; // missing clips are reported separately
                float clipLen = s.clip.length;
                if (Mathf.Abs(clipLen - lengthSeconds) > 0.01f)
                {
                    firstMismatch = string.IsNullOrEmpty(s.id) ? s.name : s.id;
                    return false;
                }
            }
            return true;
        }
    }
}
