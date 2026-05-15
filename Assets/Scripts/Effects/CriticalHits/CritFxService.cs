using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.CriticalHits
{
    /// <summary>
    /// Singleton facade for "make a critical hit feel like a JACKPOT".
    ///
    /// External callers (weapons, damage pipelines, etc.) only ever touch
    /// <see cref="OnCrit(Vector3, float)"/>. They never need to know about the
    /// popup pool, the flash overlay, the hit-stop, the impulse source, or the
    /// spark prefab — wiring those is local to this folder.
    ///
    /// The service is intentionally decoupled from the crit-ROLL system that
    /// a sibling agent owns under <c>Assets/Scripts/Combat/Crits/</c>. That
    /// system decides "is this hit a crit and how much damage?"; this service
    /// only handles the audio-visual response when something else has already
    /// decided it is.
    ///
    /// Wiring this in a scene:
    ///   - Create a GameObject (e.g. "CritFxService") and attach this script.
    ///   - Assign <see cref="critPopupPrefab"/> to the prefab built by
    ///     <c>LoneFighter -> FX -> Generate Crit Popup Prefab</c>.
    ///   - Assign a <see cref="CritFlash"/> instance (full-screen Image overlay
    ///     on a Canvas) and/or a <see cref="CritImpactBurst"/> instance (any
    ///     GameObject; uses <see cref="CritImpactBurst.Instance"/> if left null).
    ///   - Either tick <see cref="useFxServiceShake"/> to piggy-back the
    ///     project's central camera shake (FxService.HeavyShake), or assign a
    ///     local <see cref="Unity.Cinemachine.CinemachineImpulseSource"/>.
    /// </summary>
    public class CritFxService : MonoBehaviour
    {
        public static CritFxService Instance { get; private set; }

        [Header("Floating Number")]
        [Tooltip("Prefab built by 'LoneFighter -> FX -> Generate Crit Popup Prefab'. Must contain a CritDamagePopup + TMP_Text.")]
        [SerializeField] private GameObject critPopupPrefab;
        [Tooltip("World-space offset applied when spawning the popup so it pops above the hit point.")]
        [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.4f, 0f);
        [Tooltip("Random horizontal jitter applied to the popup so stacked crits don't perfectly overlap.")]
        [SerializeField] private float popupHorizontalJitter = 0.18f;

        [Header("Full-Screen Flash")]
        [Tooltip("Optional. If null, falls back to any CritFlash component found in the scene at start.")]
        [SerializeField] private CritFlash flash;

        [Header("Sparks")]
        [Tooltip("Optional. If null, uses CritImpactBurst.Instance at call time.")]
        [SerializeField] private CritImpactBurst impactBurst;

        [Header("Camera Shake")]
        [Tooltip("If true, calls FxService.Instance.HeavyShake() (recommended — keeps shake centralized).")]
        [SerializeField] private bool useFxServiceShake = true;

        [Header("Hit-Stop")]
        [Tooltip("Duration in unscaled seconds of the freeze-frame on each crit. Set to 0 to disable.")]
        [SerializeField, Range(0f, 0.2f)] private float freezeSeconds = CritFreezeFrame.DEFAULT_DURATION;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Late-resolve references so scene authors aren't forced to wire them
            // explicitly when there's a single obvious candidate.
            if (flash == null)
            {
#if UNITY_2022_2_OR_NEWER
                flash = FindFirstObjectByType<CritFlash>();
#else
                flash = FindObjectOfType<CritFlash>();
#endif
            }
            if (impactBurst == null)
            {
                impactBurst = CritImpactBurst.Instance;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Fire the full JACKPOT response for a single critical hit.
        ///
        /// Order of operations is chosen deliberately:
        ///   1. Hit-stop FIRST — captures any subsequent calls inside the same
        ///      frame inside the same freeze window instead of double-pausing.
        ///   2. Sparks at world position.
        ///   3. Full-screen flash (unscaled-time, still animates during freeze).
        ///   4. Camera shake (impulses are time-scale aware but Cinemachine
        ///      still queues them).
        ///   5. Popup — last so the text appears above sparks/flash visually.
        /// </summary>
        /// <param name="worldPos">Where the crit landed in world space.</param>
        /// <param name="damage">Damage value to display in the popup.</param>
        public void OnCrit(Vector3 worldPos, float damage)
        {
            // 1. Micro hit-stop
            CritFreezeFrame.Apply(freezeSeconds);

            // 2. Sparks
            var burst = impactBurst != null ? impactBurst : CritImpactBurst.Instance;
            if (burst != null) burst.Play(worldPos);

            // 3. Flash
            if (flash != null) flash.Play();

            // 4. Heavy camera shake
            if (useFxServiceShake && FxService.Instance != null)
            {
                FxService.Instance.HeavyShake();
            }

            // 5. Popup
            SpawnPopup(worldPos, damage);
        }

        private void SpawnPopup(Vector3 worldPos, float damage)
        {
            if (critPopupPrefab == null) return;

            float jitter = popupHorizontalJitter > 0f
                ? Random.Range(-popupHorizontalJitter, popupHorizontalJitter)
                : 0f;
            Vector3 spawnPos = worldPos + popupOffset + new Vector3(jitter, 0f, 0f);

            GameObject instance;
            if (PoolService.Instance != null)
            {
                instance = PoolService.Instance.Get(critPopupPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                instance = Instantiate(critPopupPrefab, spawnPos, Quaternion.identity);
            }
            if (instance == null) return;

            var popup = instance.GetComponent<CritDamagePopup>();
            if (popup != null) popup.Show(damage);
        }
    }
}
