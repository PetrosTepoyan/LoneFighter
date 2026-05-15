using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Effects.CameraJuice
{
    /// <summary>
    /// Directional camera "kick" — a brief, springy offset of the Cinemachine
    /// camera in a specified world direction, then a damped return to zero.
    /// Composes with shake/impulse: this writes a *local position offset* on the
    /// camera transform, separate from <see cref="CinemachineImpulseSource"/>.
    ///
    /// Two auto-subscribed sources:
    /// <list type="bullet">
    /// <item><description>Player damage — taps <see cref="PlayerHealth.OnHealthChanged"/>;
    /// since the project does not expose a "last damage source" hook, we kick
    /// in a random unit direction by default.</description></item>
    /// <item><description>Player firing — call <see cref="KickFromFire(Vector2)"/>
    /// (or the facade <see cref="CameraJuiceService.OnFire(Vector2)"/>) with the
    /// outgoing projectile direction; the camera gets a small opposite kick.</description></item>
    /// </list>
    ///
    /// Unscaled-time driven so it lives through hit-stop and slow-mo.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class CameraKick : MonoBehaviour
    {
        public static CameraKick Instance { get; private set; }

        [Header("Target Camera")]
        [Tooltip("Cinemachine 3.x camera whose transform.localPosition will be offset.")]
        [SerializeField] private CinemachineCamera targetCamera;

        [Header("Damage Kick")]
        [SerializeField] private bool autoSubscribeDamage = true;
        [SerializeField, Min(0f)] private float damageKickAmplitude = 0.45f;
        [SerializeField, Min(0.02f)] private float damageKickDuration = 0.25f;

        [Header("Fire Kick")]
        [SerializeField, Min(0f)] private float fireKickAmplitude = 0.08f;
        [SerializeField, Min(0.02f)] private float fireKickDuration = 0.08f;

        [Header("Clamping")]
        [Tooltip("Hard cap on cumulative kick magnitude in world units.")]
        [SerializeField, Min(0f)] private float maxKickOffset = 1.5f;

        private Transform _camTransform;
        private Vector3 _baseLocalPos;
        private bool _capturedBase;

        private Vector2 _currentOffset;
        private Vector2 _velocity; // for damped spring

        private PlayerHealth _health;
        private float _lastHealth = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (targetCamera == null) targetCamera = GetComponent<CinemachineCamera>();
            if (targetCamera == null) targetCamera = FindFirstObjectByType<CinemachineCamera>();
            if (targetCamera != null) _camTransform = targetCamera.transform;
        }

        private void Start()
        {
            CaptureBase();
            if (autoSubscribeDamage)
            {
                if (PlayerController.Instance != null)
                {
                    _health = PlayerController.Instance.GetComponent<PlayerHealth>();
                }
                if (_health == null) _health = FindFirstObjectByType<PlayerHealth>();
                if (_health != null)
                {
                    _lastHealth = _health.Current;
                    _health.OnHealthChanged += HandleHealthChanged;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            RestoreBase();
        }

        private void CaptureBase()
        {
            if (_capturedBase || _camTransform == null) return;
            _baseLocalPos = _camTransform.localPosition;
            _capturedBase = true;
        }

        private void RestoreBase()
        {
            if (!_capturedBase || _camTransform == null) return;
            _camTransform.localPosition = _baseLocalPos;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastHealth < 0f)
            {
                _lastHealth = current;
                return;
            }
            if (current < _lastHealth)
            {
                // Damage taken. No source available -> random unit direction.
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                KickFromDamage(dir);
            }
            _lastHealth = current;
        }

        /// <summary>
        /// Kick the camera opposite to <paramref name="incomingDir"/> with the
        /// configured damage amplitude. Pass a unit vector pointing FROM the
        /// damage source TO the player (i.e. the velocity of the incoming hit).
        /// </summary>
        public void KickFromDamage(Vector2 incomingDir)
        {
            Kick(-incomingDir.normalized, damageKickAmplitude, damageKickDuration);
        }

        /// <summary>
        /// Kick the camera opposite to <paramref name="fireDir"/> with the
        /// configured fire amplitude. Pass the weapon's outgoing direction.
        /// </summary>
        public void KickFromFire(Vector2 fireDir)
        {
            Kick(-fireDir.normalized, fireKickAmplitude, fireKickDuration);
        }

        /// <summary>
        /// Generic kick: <paramref name="direction"/> is the desired direction of
        /// the camera offset; <paramref name="amplitude"/> is the magnitude.
        /// Uses an instant impulse + critically-damped spring return.
        /// </summary>
        public void Kick(Vector2 direction, float amplitude, float duration)
        {
            if (_camTransform == null) return;
            if (amplitude <= 0f || duration <= 0f) return;
            if (direction.sqrMagnitude < 0.0001f) return;

            CaptureBase();

            Vector2 impulse = direction.normalized * amplitude;
            // Add to velocity so simultaneous kicks compose physically.
            _velocity += impulse / Mathf.Max(0.01f, duration);
        }

        private void LateUpdate()
        {
            if (_camTransform == null) return;
            CaptureBase();

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // Critically-damped-ish spring: stiffness chosen to settle in ~0.25s.
            const float stiffness = 120f;
            const float damping = 22f;

            Vector2 accel = -stiffness * _currentOffset - damping * _velocity;
            _velocity += accel * dt;
            _currentOffset += _velocity * dt;

            if (_currentOffset.magnitude > maxKickOffset)
            {
                _currentOffset = _currentOffset.normalized * maxKickOffset;
            }

            // Snap to rest when tiny — avoids subpixel jitter forever.
            if (_currentOffset.sqrMagnitude < 1e-6f && _velocity.sqrMagnitude < 1e-4f)
            {
                _currentOffset = Vector2.zero;
                _velocity = Vector2.zero;
                _camTransform.localPosition = _baseLocalPos;
                return;
            }

            _camTransform.localPosition = _baseLocalPos + (Vector3)_currentOffset;
        }
    }
}
