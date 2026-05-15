// WorldAudioPool.cs
// LoneFighter — Spatial Audio module
// Singleton that pre-allocates a fixed-size pool of SpatialAudioSource instances
// and serves world-positioned SFX requests. Sources are auto-returned to the pool
// after the clip's duration elapses (taking pitch into account).
//
// This pool is parallel to AudioManager — AudioManager continues to handle UI,
// music, and 2D player-centric SFX; WorldAudioPool only handles 3D world events.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Audio.Spatial
{
    [DisallowMultipleComponent]
    public sealed class WorldAudioPool : MonoBehaviour
    {
        public const int PoolSize = 24;

        private static WorldAudioPool _instance;

        /// <summary>
        /// Singleton accessor. Auto-creates a hidden GameObject on first access
        /// so callers never need to drag this into the scene.
        /// </summary>
        public static WorldAudioPool Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // Search the scene first in case a designer placed one manually.
                _instance = FindFirstObjectByType<WorldAudioPool>();
                if (_instance != null) return _instance;

                var go = new GameObject("[WorldAudioPool]");
                _instance = go.AddComponent<WorldAudioPool>();
                return _instance;
            }
        }

        private readonly Queue<SpatialAudioSource> _free = new Queue<SpatialAudioSource>(PoolSize);
        private readonly HashSet<SpatialAudioSource> _inUse = new HashSet<SpatialAudioSource>();
        private bool _initialized;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject($"WorldAudio_{i:00}");
                child.transform.SetParent(transform, worldPositionStays: false);
                // AddComponent<SpatialAudioSource> triggers AddComponent<AudioSource> via RequireComponent.
                var sas = child.AddComponent<SpatialAudioSource>();
                _free.Enqueue(sas);
            }
        }

        /// <summary>
        /// Play a clip at a world position via the pool. If the pool is fully
        /// saturated, the oldest in-use source is stolen (voice-stealing).
        /// </summary>
        public void PlayClipAtPoint(Vector3 pos, AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            EnsureInitialized();

            var src = AcquireInternal();
            if (src == null) return;

            src.PlayAt(pos, clip, volume, pitch);
            StartCoroutine(ReleaseAfter(src, clip, pitch));
        }

        /// <summary>
        /// Acquire a source for caller-managed lifetime (e.g. boss rumble loops).
        /// Caller MUST call <see cref="Release"/> when finished, or the source will leak.
        /// Returns null only if the pool has been destroyed.
        /// </summary>
        public SpatialAudioSource Acquire()
        {
            EnsureInitialized();
            return AcquireInternal();
        }

        /// <summary>Stop and return a source previously obtained via <see cref="Acquire"/>.</summary>
        public void Release(SpatialAudioSource src)
        {
            if (src == null) return;
            if (!_inUse.Remove(src))
            {
                // Already released, or not from this pool — ignore.
                return;
            }
            src.StopAndClear();
            _free.Enqueue(src);
        }

        private SpatialAudioSource AcquireInternal()
        {
            SpatialAudioSource src = null;

            if (_free.Count > 0)
            {
                src = _free.Dequeue();
            }
            else
            {
                // Voice steal: grab any in-use source. HashSet enumeration order
                // is not defined but is good enough for a 24-voice pool.
                foreach (var s in _inUse)
                {
                    src = s;
                    break;
                }
                if (src != null)
                {
                    _inUse.Remove(src);
                    src.StopAndClear();
                }
            }

            if (src != null)
            {
                _inUse.Add(src);
            }
            return src;
        }

        private IEnumerator ReleaseAfter(SpatialAudioSource src, AudioClip clip, float pitch)
        {
            // Clip duration scales inversely with pitch (pitch=2 plays in half the time).
            float safePitch = Mathf.Max(0.01f, Mathf.Abs(pitch));
            float duration  = clip.length / safePitch;
            // Small epsilon so we don't cut off the final sample on the very edge.
            yield return new WaitForSeconds(duration + 0.05f);

            if (src != null && _inUse.Contains(src))
            {
                Release(src);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
