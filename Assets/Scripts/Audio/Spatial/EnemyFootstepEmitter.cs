// EnemyFootstepEmitter.cs
// LoneFighter — Spatial Audio module
// Plays footstep SFX at the enemy's position whenever the enemy is moving.
// Uses Rigidbody2D.linearVelocity to detect motion (Unity 6 / Physics 2D —
// `velocity` was renamed to `linearVelocity`).
//
// Attach to any moving enemy GameObject. The enemy does not need to know
// this exists — no calls into existing enemy scripts are made.

using UnityEngine;

namespace LoneFighter.Audio.Spatial
{
    [DisallowMultipleComponent]
    public sealed class EnemyFootstepEmitter : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Footstep clips. One is chosen at random per step.")]
        [SerializeField] private AudioClip[] _footstepClips;

        [Header("Timing")]
        [Tooltip("Seconds between footsteps while moving.")]
        [SerializeField] private float _interval = 0.35f;

        [Tooltip("Minimum |linearVelocity| (units/sec) considered 'moving'.")]
        [SerializeField] private float _movementThreshold = 0.05f;

        [Header("Mix")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.6f;

        [Tooltip("Random pitch range applied per-emitter and per-step.")]
        [SerializeField] private Vector2 _pitchRange = new Vector2(0.92f, 1.08f);

        [Tooltip("If true, this emitter picks a fixed base pitch on Start so each individual enemy has a recognizable footstep tone. Per-step jitter is then small.")]
        [SerializeField] private bool _stableBasePitch = true;

        private Rigidbody2D _rb;
        private float _nextStepTime;
        private float _basePitch = 1f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            if (_stableBasePitch)
            {
                _basePitch = Random.Range(_pitchRange.x, _pitchRange.y);
            }
            // Stagger first step so a wave of enemies doesn't fire in unison.
            _nextStepTime = Time.time + Random.Range(0f, _interval);
        }

        private void Update()
        {
            if (_footstepClips == null || _footstepClips.Length == 0) return;
            if (Time.time < _nextStepTime) return;
            if (!IsMoving()) return;

            EmitFootstep();
            _nextStepTime = Time.time + _interval;
        }

        private bool IsMoving()
        {
            if (_rb == null) return false;
            // Unity 6 API: linearVelocity (replaces the obsolete `velocity`).
            return _rb.linearVelocity.sqrMagnitude > (_movementThreshold * _movementThreshold);
        }

        private void EmitFootstep()
        {
            var pool = WorldAudioPool.Instance;
            if (pool == null) return;

            var clip = _footstepClips[Random.Range(0, _footstepClips.Length)];
            if (clip == null) return;

            float pitch;
            if (_stableBasePitch)
            {
                // Small jitter around the per-enemy base pitch.
                pitch = _basePitch + Random.Range(-0.03f, 0.03f);
            }
            else
            {
                pitch = Random.Range(_pitchRange.x, _pitchRange.y);
            }

            pool.PlayClipAtPoint(transform.position, clip, _volume, pitch);
        }
    }
}
