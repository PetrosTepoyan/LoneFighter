// BossRumbleEmitter.cs
// LoneFighter — Spatial Audio module
// Continuous low-rumble spatial loop attached to bosses. Uses a dedicated
// SpatialAudioSource acquired from WorldAudioPool with loop = true.
// The source's transform tracks the boss every LateUpdate so the rumble
// pans correctly as the boss moves.
//
// The source is released back to the pool on disable / destroy so the pool
// does not leak voices when a boss dies.

using UnityEngine;

namespace LoneFighter.Audio.Spatial
{
    [DisallowMultipleComponent]
    public sealed class BossRumbleEmitter : MonoBehaviour
    {
        [Header("Clip")]
        [Tooltip("Looping low-rumble clip. Should be a seamless loop.")]
        [SerializeField] private AudioClip _rumbleClip;

        [Header("Mix")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.5f;

        [Range(0.5f, 1.5f)]
        [SerializeField] private float _pitch = 1f;

        [Tooltip("If true, fade volume in over FadeInTime seconds when enabled.")]
        [SerializeField] private bool _fadeIn = true;

        [SerializeField] private float _fadeInTime = 0.75f;

        private SpatialAudioSource _source;
        private float _enableTime;

        private void OnEnable()
        {
            _enableTime = Time.time;
            AcquireAndStart();
        }

        private void OnDisable()
        {
            ReleaseIfHeld();
        }

        private void OnDestroy()
        {
            ReleaseIfHeld();
        }

        private void LateUpdate()
        {
            if (_source == null) return;

            // Follow the boss every frame so panning is accurate.
            _source.transform.position = transform.position;

            // Handle fade-in.
            float vol = _volume;
            if (_fadeIn && _fadeInTime > 0f)
            {
                float t = Mathf.Clamp01((Time.time - _enableTime) / _fadeInTime);
                vol *= t;
            }

            var inner = _source.Source;
            if (inner != null)
            {
                inner.volume       = vol;
                inner.spatialBlend = SpatialAudioSettings.Blend;
                // If something external stopped it, restart.
                if (!inner.isPlaying && _rumbleClip != null && isActiveAndEnabled)
                {
                    inner.Play();
                }
            }
        }

        private void AcquireAndStart()
        {
            if (_rumbleClip == null) return;

            var pool = WorldAudioPool.Instance;
            if (pool == null) return;

            _source = pool.Acquire();
            if (_source == null) return;

            // PlayLoopAt configures spatialBlend, loop=true, volume, pitch, etc.
            _source.PlayLoopAt(transform, _rumbleClip, _fadeIn ? 0f : _volume, _pitch);
        }

        private void ReleaseIfHeld()
        {
            if (_source == null) return;
            var pool = WorldAudioPool.Instance;
            if (pool != null)
            {
                pool.Release(_source);
            }
            else
            {
                _source.StopAndClear();
            }
            _source = null;
        }
    }
}
