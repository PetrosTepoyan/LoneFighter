using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Effects.CameraJuice
{
    /// <summary>
    /// Camera "punch zoom" — snaps the Cinemachine 3.x orthographic lens in by
    /// <c>intensity</c> for the first half of <c>duration</c>, then eases back to
    /// the resting size over the second half. Stacks safely: re-triggering during
    /// an active punch adopts whichever target is *deeper* and extends the
    /// outgoing ease accordingly.
    ///
    /// The component must be attached to the same GameObject as a
    /// <see cref="CinemachineCamera"/>, or the camera must be assigned manually.
    /// Uses <c>unscaledDeltaTime</c> so the punch survives hit-stop and slow-mo.
    ///
    /// Auto-subscribes to <see cref="PlayerLevel.OnLevelUp"/> for a small punch.
    /// Boss spawn / boss death punches are driven externally through
    /// <see cref="PunchBossSpawn"/> / <see cref="PunchBossDeath"/> (or via
    /// <see cref="CameraJuiceService"/>) — the project does not currently expose
    /// boss-spawn / boss-death events at compile time.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraPunchZoom : MonoBehaviour
    {
        public static CameraPunchZoom Instance { get; private set; }

        [Header("Target Camera")]
        [Tooltip("Cinemachine 3.x camera whose lens.OrthographicSize will be punched. " +
                 "Auto-resolved at Awake from this GameObject, then via FindFirstObjectByType.")]
        [SerializeField] private CinemachineCamera targetCamera;

        [Header("Preset Intensities (units of orthographic-size, subtracted from rest size)")]
        [SerializeField, Min(0f)] private float smallPunchIntensity = 0.4f;
        [SerializeField, Min(0f)] private float mediumPunchIntensity = 0.9f;
        [SerializeField, Min(0f)] private float heavyPunchIntensity = 1.6f;

        [Header("Preset Durations (seconds, unscaled)")]
        [SerializeField, Min(0.02f)] private float smallPunchDuration = 0.25f;
        [SerializeField, Min(0.02f)] private float mediumPunchDuration = 0.45f;
        [SerializeField, Min(0.02f)] private float heavyPunchDuration = 0.7f;

        [Header("Auto Subscriptions")]
        [SerializeField] private bool autoSubscribeLevelUp = true;

        [Header("Safety")]
        [Tooltip("Lower bound for orthographic size after a punch — prevents punching past zero.")]
        [SerializeField, Min(0.1f)] private float minOrthoSize = 0.5f;

        private float _restSize = -1f;
        private float _activeIntensity;
        private Coroutine _routine;
        private PlayerLevel _playerLevel;

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
        }

        private void Start()
        {
            CaptureRestSizeIfNeeded();
            ResolveSubscriptions();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_playerLevel != null) _playerLevel.OnLevelUp -= HandleLevelUp;
            RestoreRestSize();
        }

        private void ResolveSubscriptions()
        {
            if (!autoSubscribeLevelUp) return;
            if (PlayerController.Instance != null)
            {
                _playerLevel = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
            if (_playerLevel == null)
            {
                _playerLevel = FindFirstObjectByType<PlayerLevel>();
            }
            if (_playerLevel != null) _playerLevel.OnLevelUp += HandleLevelUp;
        }

        private void HandleLevelUp(int _) => PunchSmall();

        /// <summary>Small punch — used for level-ups.</summary>
        public void PunchSmall() => Punch(smallPunchIntensity, smallPunchDuration);

        /// <summary>Medium punch — used for boss spawn.</summary>
        public void PunchBossSpawn() => Punch(mediumPunchIntensity, mediumPunchDuration);

        /// <summary>Heavy punch — used for boss death.</summary>
        public void PunchBossDeath() => Punch(heavyPunchIntensity, heavyPunchDuration);

        /// <summary>
        /// Generic entry point. <paramref name="intensity"/> is subtracted from the
        /// resting orthographic size; <paramref name="duration"/> is split evenly
        /// between snap-in and ease-out.
        /// </summary>
        public void Punch(float intensity, float duration)
        {
            if (targetCamera == null) return;
            if (intensity <= 0f || duration <= 0f) return;

            CaptureRestSizeIfNeeded();

            // Adopt deeper intensity if re-triggered during an active punch.
            if (_routine != null)
            {
                _activeIntensity = Mathf.Max(_activeIntensity, intensity);
                StopCoroutine(_routine);
            }
            else
            {
                _activeIntensity = intensity;
            }

            _routine = StartCoroutine(Run(_activeIntensity, duration));
        }

        private void CaptureRestSizeIfNeeded()
        {
            if (_restSize > 0f) return;
            if (targetCamera == null) return;
            _restSize = targetCamera.Lens.OrthographicSize;
        }

        private void RestoreRestSize()
        {
            if (_restSize <= 0f || targetCamera == null) return;
            var lens = targetCamera.Lens;
            lens.OrthographicSize = _restSize;
            targetCamera.Lens = lens;
        }

        private IEnumerator Run(float intensity, float duration)
        {
            float half = duration * 0.5f;

            // Snap-in: linear ramp to target size over the first half.
            float targetSize = Mathf.Max(minOrthoSize, _restSize - intensity);
            float startSize = targetCamera.Lens.OrthographicSize;
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                ApplySize(Mathf.Lerp(startSize, targetSize, k));
                yield return null;
            }
            ApplySize(targetSize);

            // Ease-out: cubic ease back to rest over the second half.
            float fromSize = targetCamera.Lens.OrthographicSize;
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                float eased = 1f - Mathf.Pow(1f - k, 3f); // EaseOutCubic
                ApplySize(Mathf.Lerp(fromSize, _restSize, eased));
                yield return null;
            }
            ApplySize(_restSize);

            _activeIntensity = 0f;
            _routine = null;
        }

        private void ApplySize(float size)
        {
            if (targetCamera == null) return;
            var lens = targetCamera.Lens;
            lens.OrthographicSize = size;
            targetCamera.Lens = lens;
        }
    }
}
