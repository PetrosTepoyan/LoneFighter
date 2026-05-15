using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Enemies;
using LoneFighter.Pickups;
using LoneFighter.Player;

namespace LoneFighter.UI.Minimap
{
    /// <summary>
    /// Bottom-right corner radar that plots nearby enemies, pickups, and bosses around the
    /// player. Owns a pool of <see cref="MinimapMarker"/> instances so per-frame allocations
    /// stay flat regardless of crowd size.
    ///
    /// Update loop:
    /// 1. Every <see cref="refreshEveryFrames"/> frames in LateUpdate, hide all live markers.
    /// 2. Plot the player at center (optional), then walk the enemy registry and pickup
    ///    arrays, filtering by <see cref="worldRadius"/>, converting world -> radar-local.
    /// 3. Push the in-range enemy count to <see cref="DangerProximity"/> so the background
    ///    can react to crowd density.
    ///
    /// All sub-systems (mask, sweep, danger tint, boss arrow) are opt-in references — the
    /// controller works on its own and just lights them up if they're wired or auto-built.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapController : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Rect that markers are parented to. Should be the masked circular radar region. Defaults to this transform.")]
        [SerializeField] private RectTransform radarRoot;

        [Tooltip("Background image that defines the visible radar disc. Used by danger/colors and to derive radius.")]
        [SerializeField] private Image background;

        [Tooltip("World units shown from the player to the edge of the radar.")]
        [SerializeField] private float worldRadius = 12f;

        [Tooltip("If true, the radar rotates with the player so 'forward' is always up. Plain top-down feel keeps this off.")]
        [SerializeField] private bool rotateWithPlayer = false;

        [Header("Refresh")]
        [Tooltip("How often (in frames) to redraw markers. 5 is a good mobile-friendly default at 120Hz.")]
        [Min(1)] [SerializeField] private int refreshEveryFrames = 5;

        [Header("Pool")]
        [Tooltip("Initial pool size for marker images. Grows on demand.")]
        [Min(0)] [SerializeField] private int initialPoolSize = 32;

        [Tooltip("Hard cap on visible markers per refresh. Prevents UI overdraw blowups in late-game crowds.")]
        [Min(8)] [SerializeField] private int maxVisibleMarkers = 256;

        [Header("Markers")]
        [Tooltip("Plot the player as a dot at the center of the radar.")]
        [SerializeField] private bool showPlayerMarker = true;

        [Tooltip("Plot XP gems. Disable on lower-end devices if perf becomes a concern.")]
        [SerializeField] private bool plotXpGems = true;

        [Tooltip("Plot health / magnet / bomb pickups.")]
        [SerializeField] private bool plotPickups = true;

        [Header("Optional Subsystems")]
        [Tooltip("Optional. Tints background when crowd density is high.")]
        [SerializeField] private DangerProximity dangerProximity;

        [Tooltip("Optional. Auto-creates a circle mask + procedural circle background if assigned.")]
        [SerializeField] private MinimapMaskSetup maskSetup;

        // ---- Internals ---------------------------------------------------

        private readonly List<MinimapMarker> _pool = new();
        private int _liveMarkerCount;
        private int _frameCounter;
        private MinimapMarker _playerMarker;

        private void Awake()
        {
            if (radarRoot == null) radarRoot = (RectTransform)transform;
            if (background == null) background = radarRoot.GetComponent<Image>();

            // If the user wired a mask helper, let it ensure the radar disc + mask exist.
            if (maskSetup != null)
            {
                // No-op; MinimapMaskSetup handles itself in Awake.
            }

            WarmPool(initialPoolSize);
        }

        private void OnEnable()
        {
            _frameCounter = 0;
        }

        private void LateUpdate()
        {
            _frameCounter++;
            if (_frameCounter < refreshEveryFrames) return;
            _frameCounter = 0;
            Refresh();
        }

        /// <summary>Forces an immediate refresh. Useful from tests or when the layout changes.</summary>
        public void Refresh()
        {
            HideAllMarkers();

            Transform playerTf = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            if (playerTf == null)
            {
                if (dangerProximity != null) dangerProximity.ReportEnemyCount(0);
                return;
            }

            Vector2 playerPos = playerTf.position;
            float radarPixelRadius = ComputeRadarPixelRadius();
            float invWorldRadius = worldRadius > 0.0001f ? 1f / worldRadius : 0f;

            // Optional: rotate the whole "frame" so player.right is up. We compute a 2x2 inline.
            float cos = 1f, sin = 0f;
            if (rotateWithPlayer)
            {
                // Use the player's transform's up vector as the radar "up".
                Vector2 up = playerTf.up;
                if (up.sqrMagnitude > 0.0001f)
                {
                    up.Normalize();
                    // Inverse rotation: we want world->local where local.up == player.up.
                    cos = up.y;
                    sin = -up.x;
                }
            }

            // Center marker for the player.
            if (showPlayerMarker)
            {
                _playerMarker = AcquireMarker();
                if (_playerMarker != null) _playerMarker.Configure(MinimapMarker.MarkerType.Player, Vector2.zero);
            }

            int enemiesInRange = 0;

            // --- Enemies (via registry; no allocations) ---
            var enemies = EnemyRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                Vector2 ePos = enemy.transform.position;
                Vector2 delta = ePos - playerPos;
                if (delta.sqrMagnitude > worldRadius * worldRadius) continue;

                enemiesInRange++;
                if (_liveMarkerCount >= maxVisibleMarkers) continue;

                var type = ClassifyEnemy(enemy);
                var anchored = WorldDeltaToRadar(delta, cos, sin, invWorldRadius, radarPixelRadius);
                var marker = AcquireMarker();
                if (marker != null) marker.Configure(type, anchored);
            }

            // --- XP gems ---
            if (plotXpGems && _liveMarkerCount < maxVisibleMarkers)
            {
                var gems = Object.FindObjectsByType<XPGem>(FindObjectsSortMode.None);
                for (int i = 0; i < gems.Length; i++)
                {
                    var gem = gems[i];
                    if (gem == null || !gem.isActiveAndEnabled) continue;
                    Vector2 gPos = gem.transform.position;
                    Vector2 delta = gPos - playerPos;
                    if (delta.sqrMagnitude > worldRadius * worldRadius) continue;
                    if (_liveMarkerCount >= maxVisibleMarkers) break;
                    var anchored = WorldDeltaToRadar(delta, cos, sin, invWorldRadius, radarPixelRadius);
                    var marker = AcquireMarker();
                    if (marker != null) marker.Configure(MinimapMarker.MarkerType.Xp, anchored);
                }
            }

            // --- Other pickups (Health, Magnet, Bomb) ---
            if (plotPickups && _liveMarkerCount < maxVisibleMarkers)
            {
                PlotPickupType<HealthPickup>(playerPos, cos, sin, invWorldRadius, radarPixelRadius, MinimapMarker.MarkerType.Health);
                if (_liveMarkerCount < maxVisibleMarkers)
                    PlotPickupType<MagnetPickup>(playerPos, cos, sin, invWorldRadius, radarPixelRadius, MinimapMarker.MarkerType.Pickup);
                if (_liveMarkerCount < maxVisibleMarkers)
                    PlotPickupType<BombPickup>(playerPos, cos, sin, invWorldRadius, radarPixelRadius, MinimapMarker.MarkerType.Pickup);
            }

            if (dangerProximity != null) dangerProximity.ReportEnemyCount(enemiesInRange);
        }

        private void PlotPickupType<T>(Vector2 playerPos, float cos, float sin, float invWorldRadius, float radarPixelRadius, MinimapMarker.MarkerType type)
            where T : Pickup
        {
            var items = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < items.Length; i++)
            {
                var p = items[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                Vector2 wp = p.transform.position;
                Vector2 delta = wp - playerPos;
                if (delta.sqrMagnitude > worldRadius * worldRadius) continue;
                if (_liveMarkerCount >= maxVisibleMarkers) break;
                var anchored = WorldDeltaToRadar(delta, cos, sin, invWorldRadius, radarPixelRadius);
                var marker = AcquireMarker();
                if (marker != null) marker.Configure(type, anchored);
            }
        }

        /// <summary>
        /// Classifies an enemy as Boss / Elite / Enemy based on EnemyData name + visual config.
        /// Boss detection uses the same simple substring rule as <see cref="OffscreenBossArrow"/>
        /// so the two stay in sync without sharing a flag on the ScriptableObject.
        /// </summary>
        private static MinimapMarker.MarkerType ClassifyEnemy(EnemyBase enemy)
        {
            var data = enemy.Data;
            if (data != null)
            {
                string display = data.displayName;
                string asset = data.name;
                if (NameMatchesBoss(display) || NameMatchesBoss(asset))
                    return MinimapMarker.MarkerType.Boss;
                if (NameMatchesElite(display) || NameMatchesElite(asset))
                    return MinimapMarker.MarkerType.Elite;
            }
            return MinimapMarker.MarkerType.Enemy;
        }

        private static bool NameMatchesBoss(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.IndexOf("warden", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || s.IndexOf("boss", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool NameMatchesElite(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.IndexOf("elite", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// World-space delta from the player -> radar-local anchored position in pixels.
        /// Applies an optional 2D rotation (cos/sin) when <c>rotateWithPlayer</c> is on.
        /// </summary>
        private static Vector2 WorldDeltaToRadar(Vector2 delta, float cos, float sin, float invWorldRadius, float radarPixelRadius)
        {
            // Optional rotate-with-player: rotate delta by inverse player orientation.
            float rx = delta.x * cos - delta.y * sin;
            float ry = delta.x * sin + delta.y * cos;
            // delta in world units -> normalized [-1..1] -> pixels.
            return new Vector2(rx * invWorldRadius * radarPixelRadius, ry * invWorldRadius * radarPixelRadius);
        }

        private float ComputeRadarPixelRadius()
        {
            if (radarRoot == null) return 64f;
            var size = radarRoot.rect.size;
            // Inset slightly so markers don't render exactly on the rim.
            return 0.5f * Mathf.Min(size.x, size.y) - 4f;
        }

        // ---- Pool ---------------------------------------------------------

        private void WarmPool(int count)
        {
            if (radarRoot == null) return;
            for (int i = 0; i < count; i++)
            {
                CreateMarker();
            }
        }

        private MinimapMarker CreateMarker()
        {
            if (radarRoot == null) return null;
            var go = new GameObject("Marker", typeof(RectTransform), typeof(Image), typeof(MinimapMarker));
            go.transform.SetParent(radarRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var m = go.GetComponent<MinimapMarker>();
            go.SetActive(false);
            _pool.Add(m);
            return m;
        }

        private MinimapMarker AcquireMarker()
        {
            for (int i = _liveMarkerCount; i < _pool.Count; i++)
            {
                var m = _pool[i];
                if (m == null)
                {
                    _pool.RemoveAt(i);
                    i--;
                    continue;
                }
                _liveMarkerCount++;
                return m;
            }
            var fresh = CreateMarker();
            if (fresh != null) _liveMarkerCount++;
            return fresh;
        }

        private void HideAllMarkers()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var m = _pool[i];
                if (m == null) continue;
                m.Hide();
            }
            _liveMarkerCount = 0;
            _playerMarker = null;
        }

        // ---- Public API ---------------------------------------------------

        public float WorldRadius
        {
            get => worldRadius;
            set => worldRadius = Mathf.Max(0.1f, value);
        }

        public RectTransform RadarRoot => radarRoot;
        public Image Background => background;
        public int LiveMarkerCount => _liveMarkerCount;

        public void SetRotateWithPlayer(bool rotate) => rotateWithPlayer = rotate;
    }
}
