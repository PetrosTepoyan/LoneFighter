// SpatialAudioSettings.cs
// LoneFighter — Spatial Audio module
// Static accessor for a master spatial blend value, PlayerPrefs-backed.
//
// Blend semantics:
//   1.0 = full 3D positional audio (default)
//   0.0 = full 2D fallback (mono, no positional cues)
//
// This value multiplies the AudioSource.spatialBlend of every
// SpatialAudioSource managed by WorldAudioPool. Designers / players
// can lower it for accessibility (e.g. mono / hearing-impaired mode)
// without touching the existing AudioManager.

using UnityEngine;

namespace LoneFighter.Audio.Spatial
{
    public static class SpatialAudioSettings
    {
        /// <summary>PlayerPrefs key used to persist the master spatial blend.</summary>
        public const string PrefsKey = "LF.Audio.Spatial.Blend";

        /// <summary>Default blend when nothing has been saved yet.</summary>
        public const float DefaultBlend = 1.0f;

        private static float _cached = float.NaN;

        /// <summary>
        /// Master spatial blend in [0, 1]. Reading is cached after the first
        /// access; writing updates PlayerPrefs and the cache. Reset the cache
        /// by calling <see cref="Reload"/> if PlayerPrefs is modified externally.
        /// </summary>
        public static float Blend
        {
            get
            {
                if (float.IsNaN(_cached))
                {
                    _cached = Mathf.Clamp01(
                        PlayerPrefs.GetFloat(PrefsKey, DefaultBlend));
                }
                return _cached;
            }
            set
            {
                float v = Mathf.Clamp01(value);
                _cached = v;
                PlayerPrefs.SetFloat(PrefsKey, v);
                // Note: we deliberately do NOT call PlayerPrefs.Save() on every
                // write — Unity flushes on quit. Callers that need an immediate
                // flush (e.g. settings menu apply button) can call Save().
            }
        }

        /// <summary>Persist current blend to disk immediately.</summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>Drop the cached value so the next read re-queries PlayerPrefs.</summary>
        public static void Reload()
        {
            _cached = float.NaN;
        }

        /// <summary>True if the current blend is effectively zero (2D fallback).</summary>
        public static bool Is2DFallback => Blend <= 0.0001f;
    }
}
