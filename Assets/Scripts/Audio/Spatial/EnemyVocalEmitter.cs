// EnemyVocalEmitter.cs
// LoneFighter — Spatial Audio module
// Occasionally plays a grunt / growl / hiss at the enemy's position so the
// soundscape has organic personality without spamming the listener.
//
// Random interval per vocalization: 8 – 20 seconds (configurable). Each
// enemy picks its own per-instance pitch offset on Start so individual
// enemies sound subtly distinct.

using UnityEngine;

namespace LoneFighter.Audio.Spatial
{
    [DisallowMultipleComponent]
    public sealed class EnemyVocalEmitter : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Vocal clips. One is chosen at random per vocalization.")]
        [SerializeField] private AudioClip[] _vocalClips;

        [Header("Timing")]
        [Tooltip("Minimum seconds between vocalizations.")]
        [SerializeField] private float _minInterval = 8f;

        [Tooltip("Maximum seconds between vocalizations.")]
        [SerializeField] private float _maxInterval = 20f;

        [Header("Mix")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.7f;

        [SerializeField] private Vector2 _pitchRange = new Vector2(0.9f, 1.1f);

        private float _nextVocalTime;
        private float _basePitch = 1f;

        private void Start()
        {
            _basePitch = Random.Range(_pitchRange.x, _pitchRange.y);
            // First vocalization is jittered fully so a fresh wave of spawns
            // doesn't all grunt at once.
            ScheduleNext(initial: true);
        }

        private void Update()
        {
            if (_vocalClips == null || _vocalClips.Length == 0) return;
            if (Time.time < _nextVocalTime) return;

            Emit();
            ScheduleNext(initial: false);
        }

        private void ScheduleNext(bool initial)
        {
            float lo = Mathf.Max(0.01f, _minInterval);
            float hi = Mathf.Max(lo + 0.01f, _maxInterval);
            float wait = Random.Range(lo, hi);
            // On the very first scheduling, allow the wait to be anywhere from
            // 0..hi so brand-new enemies might vocalize quickly.
            if (initial) wait = Random.Range(0f, hi);
            _nextVocalTime = Time.time + wait;
        }

        private void Emit()
        {
            var pool = WorldAudioPool.Instance;
            if (pool == null) return;

            var clip = _vocalClips[Random.Range(0, _vocalClips.Length)];
            if (clip == null) return;

            // Light jitter around the per-enemy base pitch.
            float pitch = _basePitch + Random.Range(-0.05f, 0.05f);
            pool.PlayClipAtPoint(transform.position, clip, _volume, pitch);
        }
    }
}
