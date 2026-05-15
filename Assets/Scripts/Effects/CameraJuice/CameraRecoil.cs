using Unity.Cinemachine;
using UnityEngine;

namespace LoneFighter.Effects.CameraJuice
{
    /// <summary>
    /// High-frequency rumble (~0.05 s window, tiny amplitude) intended for
    /// continuous-fire weapons like a Flamethrower. Call <see cref="Rumble"/>
    /// every frame the weapon is emitting; the rumble decays naturally once
    /// calls stop arriving.
    ///
    /// Implementation:
    ///   - Runs in <see cref="LateUpdate"/> with
    ///     <see cref="DefaultExecutionOrderAttribute"/> set high so it executes
    ///     <i>after</i> <see cref="CameraKick"/>.
    ///   - Each active frame samples a fresh jitter at <see cref="jitterHz"/>
    ///     and writes <c>baseLocalPos + currentKickOffset + jitter</c> as an
    ///     absolute position. "Current kick offset" is read by subtracting the
    ///     captured base pose from the live localPosition — that picks up
    ///     whatever <see cref="CameraKick"/> wrote this frame without coupling
    ///     to it directly.
    ///   - When not active, the script does not touch the transform.
    ///
    /// All timing is unscaled, so rumble still hums during hit-stop and slow-mo.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)] // run after CameraKick (order 100)
    public class CameraRecoil : MonoBehaviour
    {
        public static CameraRecoil Instance { get; private set; }

        [Header("Target Camera")]
        [SerializeField] private CinemachineCamera targetCamera;

        [Header("Rumble Defaults")]
        [Tooltip("Default rumble amplitude in world units (very small).")]
        [SerializeField, Range(0f, 0.2f)] private float defaultAmplitude = 0.04f;
        [Tooltip("How long a single Rumble() pulse persists if not refreshed.")]
        [SerializeField, Range(0.01f, 0.5f)] private float pulseLifetime = 0.05f;
        [Tooltip("Jitter resample rate (Hz). Higher = noisier rumble.")]
        [SerializeField, Range(20f, 240f)] private float jitterHz = 120f;

        private Transform _camTransform;
        private Vector3 _baseLocalPos;
        private bool _capturedBase;

        private float _activeAmplitude;
        private float _energy; // 0..1, decays after each Rumble pulse
        private float _nextSampleTime;
        private Vector3 _currentJitter;
        private Vector3 _lastApplied;

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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void CaptureBase()
        {
            if (_capturedBase || _camTransform == null) return;
            _baseLocalPos = _camTransform.localPosition;
            _capturedBase = true;
        }

        /// <summary>
        /// Refresh the rumble for one pulse-lifetime at the default amplitude.
        /// Call every frame the continuous weapon is firing.
        /// </summary>
        public void Rumble() => Rumble(defaultAmplitude);

        /// <summary>
        /// Refresh the rumble for one pulse-lifetime at the supplied amplitude
        /// (clamped to <see cref="defaultAmplitude"/> * 4 to keep it tiny).
        /// </summary>
        public void Rumble(float amplitude)
        {
            amplitude = Mathf.Clamp(amplitude, 0f, defaultAmplitude * 4f);
            _activeAmplitude = Mathf.Max(_activeAmplitude, amplitude);
            _energy = 1f;
        }

        private void LateUpdate()
        {
            if (_camTransform == null) return;
            CaptureBase();

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // Always remove last frame's jitter from whatever the localPosition
            // currently is. If CameraKick ran before us and wrote absolute, our
            // _lastApplied was wiped from the transform — and subtracting a
            // value already absent just biases the kick by -_lastApplied for
            // one frame, then we re-add this frame's jitter. To prevent that
            // bias, only subtract if the transform still actually contains it.
            if (_lastApplied != Vector3.zero)
            {
                // Best-effort: if the transform still has our jitter (no other
                // writer overwrote it), subtract it. We detect that by checking
                // whether the current offset matches what we wrote last frame.
                // If it doesn't, assume someone else cleaned up for us.
                Vector3 currentOffset = _camTransform.localPosition - _baseLocalPos;
                if ((currentOffset - _lastApplied).sqrMagnitude < 1e-8f)
                {
                    _camTransform.localPosition -= _lastApplied;
                }
                _lastApplied = Vector3.zero;
            }

            if (_energy <= 0f)
            {
                _activeAmplitude = 0f;
                _currentJitter = Vector3.zero;
                return;
            }

            _energy -= dt / Mathf.Max(0.01f, pulseLifetime);
            if (_energy <= 0f)
            {
                _energy = 0f;
                _activeAmplitude = 0f;
                _currentJitter = Vector3.zero;
                return;
            }

            if (Time.unscaledTime >= _nextSampleTime)
            {
                _nextSampleTime = Time.unscaledTime + 1f / Mathf.Max(1f, jitterHz);
                Vector2 sample = Random.insideUnitCircle * (_activeAmplitude * _energy);
                _currentJitter = new Vector3(sample.x, sample.y, 0f);
            }

            _camTransform.localPosition += _currentJitter;
            _lastApplied = _currentJitter;
        }
    }
}
