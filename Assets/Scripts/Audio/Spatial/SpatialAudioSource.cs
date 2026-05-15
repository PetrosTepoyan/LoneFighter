// SpatialAudioSource.cs
// LoneFighter — Spatial Audio module
// Pooled wrapper around a Unity AudioSource configured for 3D positional
// playback in a 2D top-down arena.
//
// Key configuration:
//   spatialBlend  = 1   (fully 3D — overridden per-play by SpatialAudioSettings.Blend)
//   rolloffMode   = Linear
//   minDistance   = 1
//   maxDistance   = 18
//   dopplerLevel  = 0   (the player & enemies move fast; doppler is distracting)
//
// Lifecycle: WorldAudioPool creates these via AddComponent on child GameObjects
// and recycles them by calling PlayAt(...) / StopAndRelease().

using UnityEngine;

namespace LoneFighter.Audio.Spatial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class SpatialAudioSource : MonoBehaviour
    {
        public const float DefaultMinDistance = 1f;
        public const float DefaultMaxDistance = 18f;

        private AudioSource _source;

        /// <summary>The underlying AudioSource. Useful for advanced cases (e.g. looping).</summary>
        public AudioSource Source => _source;

        /// <summary>Is this source currently producing audio?</summary>
        public bool IsPlaying => _source != null && _source.isPlaying;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            ConfigureDefaults(_source);
        }

        /// <summary>Apply the standard LoneFighter 3D-SFX configuration to an AudioSource.</summary>
        public static void ConfigureDefaults(AudioSource s)
        {
            if (s == null) return;
            s.playOnAwake   = false;
            s.loop          = false;
            s.spatialBlend  = 1f;             // Will be scaled by SpatialAudioSettings.Blend on each Play.
            s.rolloffMode   = AudioRolloffMode.Linear;
            s.minDistance   = DefaultMinDistance;
            s.maxDistance   = DefaultMaxDistance;
            s.dopplerLevel  = 0f;
            s.spread        = 0f;
            s.bypassEffects = false;
            s.bypassListenerEffects = false;
            s.bypassReverbZones = false;
            s.priority      = 128;
        }

        /// <summary>
        /// Play a one-shot clip at the given world position.
        /// </summary>
        /// <param name="position">World position of the sound event.</param>
        /// <param name="clip">Clip to play (no-op if null).</param>
        /// <param name="volumeScale">Per-play volume multiplier (clamped to [0,1]).</param>
        /// <param name="pitch">Per-play pitch (typically 0.9 - 1.1).</param>
        public void PlayAt(Vector3 position, AudioClip clip, float volumeScale, float pitch)
        {
            if (clip == null || _source == null) return;

            transform.position = position;

            _source.clip         = clip;
            _source.loop         = false;
            _source.volume       = Mathf.Clamp01(volumeScale);
            _source.pitch        = pitch;
            _source.spatialBlend = SpatialAudioSettings.Blend;
            _source.Play();
        }

        /// <summary>
        /// Configure this source for a continuous loop at a fixed transform-following position.
        /// Used by BossRumbleEmitter. Caller is responsible for Stop() / release when done.
        /// </summary>
        public void PlayLoopAt(Transform follow, AudioClip clip, float volumeScale, float pitch)
        {
            if (clip == null || _source == null) return;
            if (follow != null) transform.position = follow.position;

            _source.clip         = clip;
            _source.loop         = true;
            _source.volume       = Mathf.Clamp01(volumeScale);
            _source.pitch        = pitch;
            _source.spatialBlend = SpatialAudioSettings.Blend;
            _source.Play();
        }

        /// <summary>Stop playback and clear the clip reference. Does NOT return to a pool.</summary>
        public void StopAndClear()
        {
            if (_source == null) return;
            _source.Stop();
            _source.loop = false;
            _source.clip = null;
        }
    }
}
