using System.Collections;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Bosses
{
    /// <summary>
    /// Punchy boss death sequence: slow-mo + multi-burst shake/explosions + screen flash, then
    /// (optionally) drops a guaranteed pickup. Runs unscaled so it does not slow itself down,
    /// and snapshots <see cref="Time.timeScale"/> on entry so we don't permanently overwrite a
    /// value that some other system was already animating (e.g. FxService.HitStop).
    ///
    /// Wire-up:
    ///   * Drop this on the same GameObject as <see cref="BossEncounter"/>. <see cref="Play"/>
    ///     is called automatically when the boss dies.
    ///   * The white-flash uses a runtime full-screen Image inserted on a dedicated overlay
    ///     Canvas at sortingOrder 999 so it draws above gameplay UI. No prefab dependencies.
    ///   * Pickup drops are optional — leave the prefab fields null to skip.
    ///
    /// Important: the boss GameObject is returned to the pool / destroyed by `EnemyBase.HandleDeath`
    /// almost immediately after `OnDied` fires, which would kill any coroutine started on `this`.
    /// To survive that, <see cref="Play"/> snapshots the position and forwards the sequence to a
    /// detached runtime host (<see cref="DeathSequenceHost"/>) that owns its own lifetime.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossDeathSequence : MonoBehaviour
    {
        [Header("Slow-Mo")]
        [Tooltip("Time.timeScale during the slow-mo window.")]
        [SerializeField] private float slowMoScale = 0.3f;
        [Tooltip("Wall-clock seconds the slow-mo window lasts.")]
        [SerializeField] private float slowMoDuration = 1.5f;

        [Header("Bursts")]
        [Tooltip("Number of explosion+shake bursts to fire across the slow-mo window.")]
        [SerializeField] private int burstCount = 5;
        [Tooltip("Maximum random offset (world units) for each burst around the boss position.")]
        [SerializeField] private float burstSpread = 1.25f;

        [Header("Screen Flash")]
        [Tooltip("If true, overlay a full-screen white flash that fades out across the slow-mo window.")]
        [SerializeField] private bool whiteFlash = true;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashHoldSeconds = 0.08f;
        [SerializeField] private float flashFadeSeconds = 0.6f;

        [Header("Guaranteed Drop (optional)")]
        [Tooltip("Prefab dropped on death. Typically a BombPickup or HealthPickup.")]
        [SerializeField] private GameObject guaranteedDropPrefab;
        [Tooltip("Backup drop used if the primary prefab is null. Lets one component carry both options.")]
        [SerializeField] private GameObject fallbackDropPrefab;

        private bool _playing;

        public void Play(BossEncounter encounter)
        {
            if (_playing) return;
            _playing = true;

            // Snapshot tuning into a plain struct so the host can run independently after this
            // GameObject is pooled/destroyed by EnemyBase.HandleDeath.
            var config = new DeathSequenceHost.Config
            {
                origin = transform.position,
                slowMoScale = Mathf.Clamp(slowMoScale, 0.05f, 1f),
                slowMoDuration = Mathf.Max(0.1f, slowMoDuration),
                burstCount = Mathf.Max(1, burstCount),
                burstSpread = burstSpread,
                whiteFlash = whiteFlash,
                flashColor = flashColor,
                flashHold = flashHoldSeconds,
                flashFade = flashFadeSeconds,
                dropPrefab = guaranteedDropPrefab != null ? guaranteedDropPrefab : fallbackDropPrefab,
            };

            DeathSequenceHost.Run(config);
        }
    }

    /// <summary>
    /// Detached, self-destroying runtime host that owns the death-sequence coroutine. Created
    /// fresh per boss death; survives the boss's pool release / destroy.
    /// </summary>
    internal sealed class DeathSequenceHost : MonoBehaviour
    {
        public struct Config
        {
            public Vector3 origin;
            public float slowMoScale;
            public float slowMoDuration;
            public int burstCount;
            public float burstSpread;
            public bool whiteFlash;
            public Color flashColor;
            public float flashHold;
            public float flashFade;
            public GameObject dropPrefab;
        }

        public static void Run(Config cfg)
        {
            var go = new GameObject("BossDeathSequenceHost");
            DontDestroyOnLoad(go); // survive a scene transition during the 1.5s window
            var host = go.AddComponent<DeathSequenceHost>();
            host.StartCoroutine(host.RunSequence(cfg));
        }

        private IEnumerator RunSequence(Config cfg)
        {
            // Drop pickup first so even if the rest of the sequence is interrupted (scene change,
            // game-over), the player still gets their reward.
            DropPickup(cfg);

            float originalTimeScale = Time.timeScale;
            Time.timeScale = cfg.slowMoScale;

            if (cfg.whiteFlash) StartCoroutine(WhiteFlashRoutine(cfg));

            float interval = cfg.slowMoDuration / cfg.burstCount;
            for (int i = 0; i < cfg.burstCount; i++)
            {
                Vector3 jitter = new Vector3(
                    Random.Range(-cfg.burstSpread, cfg.burstSpread),
                    Random.Range(-cfg.burstSpread, cfg.burstSpread),
                    0f);
                Vector3 burstPos = cfg.origin + jitter;

                if (FxService.Instance != null)
                {
                    FxService.Instance.EnemyExplosion(burstPos);
                    FxService.Instance.HeavyShake();
                }

                yield return new WaitForSecondsRealtime(interval);
            }

            // Restore time-scale. If something else stomped it to 0 mid-sequence (hit-stop),
            // resume at 1 rather than freezing the game.
            Time.timeScale = (originalTimeScale > 0.01f) ? originalTimeScale : 1f;

            // Give the flash routine a beat to finish before self-destructing.
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, cfg.flashHold + cfg.flashFade));
            Destroy(gameObject);
        }

        private IEnumerator WhiteFlashRoutine(Config cfg)
        {
            GameObject canvasGO = new GameObject("BossDeathFlashCanvas");
            DontDestroyOnLoad(canvasGO);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();

            GameObject imgGO = new GameObject("Flash");
            imgGO.transform.SetParent(canvasGO.transform, false);
            var img = imgGO.AddComponent<UnityEngine.UI.Image>();
            img.color = cfg.flashColor;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            float hold = Mathf.Max(0f, cfg.flashHold);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            float fade = Mathf.Max(0.01f, cfg.flashFade);
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(cfg.flashColor.a, 0f, t / fade);
                Color c = cfg.flashColor; c.a = alpha;
                img.color = c;
                yield return null;
            }

            Destroy(canvasGO);
        }

        private static void DropPickup(Config cfg)
        {
            if (cfg.dropPrefab == null) return;
            if (PoolService.Instance != null)
                PoolService.Instance.Get(cfg.dropPrefab, cfg.origin, Quaternion.identity);
            else
                Instantiate(cfg.dropPrefab, cfg.origin, Quaternion.identity);
        }
    }
}
