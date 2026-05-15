using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LoneFighter.Polish
{
    public enum HapticIntensity { Light, Medium, Heavy }

    /// Cross-platform haptic pulse wrapper.
    /// - Android: Handheld.Vibrate() for Medium/Heavy (skip Light to spare battery).
    /// - iOS: Handheld.Vibrate() as graceful fallback. Real CoreHaptics needs a native plugin.
    /// - Gamepad: SetMotorSpeeds, auto-stopped after gamepadPulseSeconds via coroutine.
    /// - Editor / Desktop: log only, throttled to avoid spam.
    public class HapticsService : MonoBehaviour
    {
        public static HapticsService Instance { get; private set; }

        [Header("Gamepad")]
        [SerializeField, Range(0f, 0.5f)] private float gamepadPulseSeconds = 0.08f;
        [SerializeField, Range(0f, 1f)] private float gamepadLightLow = 0.15f;
        [SerializeField, Range(0f, 1f)] private float gamepadLightHigh = 0.10f;
        [SerializeField, Range(0f, 1f)] private float gamepadMediumLow = 0.35f;
        [SerializeField, Range(0f, 1f)] private float gamepadMediumHigh = 0.30f;
        [SerializeField, Range(0f, 1f)] private float gamepadHeavyLow = 0.75f;
        [SerializeField, Range(0f, 1f)] private float gamepadHeavyHigh = 0.70f;

        [Header("Debug")]
        [Tooltip("In Editor/Desktop, log haptic calls (rate-limited).")]
        [SerializeField] private bool logInEditor = false;

        private Coroutine _gamepadStopRoutine;
        private float _lastLogTime = -10f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Make sure we leave the gamepad quiet.
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        }

        public void Pulse(HapticIntensity intensity)
        {
            // Gamepad first — if connected, prefer that.
            var pad = Gamepad.current;
            if (pad != null)
            {
                PulseGamepad(pad, intensity);
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (intensity != HapticIntensity.Light)
            {
                Handheld.Vibrate();
            }
            return;
#elif UNITY_IOS && !UNITY_EDITOR
            // Fallback. Real iOS haptics (UIImpactFeedbackGenerator) require a native plugin.
            if (intensity != HapticIntensity.Light)
            {
                Handheld.Vibrate();
            }
            return;
#else
            if (logInEditor && Time.unscaledTime - _lastLogTime > 0.25f)
            {
                _lastLogTime = Time.unscaledTime;
                Debug.Log($"[Haptics] Pulse({intensity})");
            }
#endif
        }

        private void PulseGamepad(Gamepad pad, HapticIntensity intensity)
        {
            float low, high;
            switch (intensity)
            {
                case HapticIntensity.Light:
                    low = gamepadLightLow; high = gamepadLightHigh; break;
                case HapticIntensity.Medium:
                    low = gamepadMediumLow; high = gamepadMediumHigh; break;
                case HapticIntensity.Heavy:
                default:
                    low = gamepadHeavyLow; high = gamepadHeavyHigh; break;
            }
            pad.SetMotorSpeeds(low, high);
            if (_gamepadStopRoutine != null) StopCoroutine(_gamepadStopRoutine);
            _gamepadStopRoutine = StartCoroutine(StopGamepadAfter(pad, gamepadPulseSeconds));
        }

        private IEnumerator StopGamepadAfter(Gamepad pad, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (pad != null) pad.SetMotorSpeeds(0f, 0f);
            _gamepadStopRoutine = null;
        }
    }
}
