using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Enemies;

namespace LoneFighter.UI.Minimap
{
    /// <summary>
    /// Pins an arrow to the inside of the screen edge that points at the nearest alive
    /// boss when the boss is offscreen. Hides itself when the boss is on-screen or
    /// when no boss is alive.
    ///
    /// Boss detection is a simple string heuristic on <c>EnemyData.displayName</c> /
    /// asset name: anything called "Warden" or containing "Boss" qualifies. This avoids
    /// adding a flag to <c>EnemyData</c> (which is owned by other systems) and works
    /// for the current mini-boss (<see cref="WardenBoss"/>).
    ///
    /// Lives on a Screen-Space-Overlay UI canvas (typically the same canvas as the
    /// minimap). The arrow's <see cref="RectTransform"/> is anchored at (0,0)..(0,0)
    /// with pivot at center so we can drive its absolute screen position directly.
    /// </summary>
    [DisallowMultipleComponent]
    public class OffscreenBossArrow : MonoBehaviour
    {
        [Header("Arrow")]
        [Tooltip("Arrow image. If null, a triangle is generated procedurally at runtime.")]
        [SerializeField] private Image arrowImage;

        [Tooltip("Size of the arrow in pixels (used when auto-building the triangle).")]
        [SerializeField] private float arrowSize = 48f;

        [Tooltip("Tint applied to the arrow. Defaults to boss-purple for parity with the minimap marker.")]
        [SerializeField] private Color arrowColor = new Color(0.85f, 0.45f, 1f, 1f);

        [Header("Screen Placement")]
        [Tooltip("Distance (px) the arrow stays inset from the edge of the screen.")]
        [SerializeField] private float screenPadding = 64f;

        [Tooltip("Camera used to project world -> screen. Defaults to Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Header("Detection")]
        [Tooltip("Substrings that mark an enemy as a boss (case-insensitive). Default: 'Warden', 'Boss'.")]
        [SerializeField] private string[] bossNameTokens = new[] { "Warden", "Boss" };

        [Tooltip("Refresh interval in seconds. Bosses are rare so we don't need per-frame updates.")]
        [SerializeField] private float refreshInterval = 0.1f;

        private RectTransform _rt;
        private Canvas _canvas;
        private float _nextRefreshTime;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            if (worldCamera == null) worldCamera = Camera.main;
            EnsureArrowImage();
            Hide();
        }

        private void OnEnable()
        {
            _nextRefreshTime = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime) return;
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.016f, refreshInterval);

            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Hide();
                return;
            }

            var boss = FindNearestBoss();
            if (boss == null)
            {
                Hide();
                return;
            }

            Vector3 bossWorld = boss.transform.position;
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(bossWorld);

            // Behind the camera in perspective rigs: WorldToScreenPoint returns negative z.
            // Flip into a sensible vector so the arrow still aims at the boss.
            if (screenPoint.z < 0f)
            {
                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            bool onScreen = screenPoint.z > 0f
                            && screenPoint.x >= 0f && screenPoint.x <= Screen.width
                            && screenPoint.y >= 0f && screenPoint.y <= Screen.height;

            if (onScreen)
            {
                Hide();
                return;
            }

            // Clamp the screen-space target into the visible rect with padding, then aim at it.
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = (Vector2)screenPoint - screenCenter;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

            // Intersect the direction with the padded screen rect to find where to draw the arrow.
            Vector2 anchor = ClampToScreenEdge(screenCenter, dir, screenPadding);

            PlaceArrow(anchor, dir);
            Show();
        }

        private EnemyBase FindNearestBoss()
        {
            var enemies = EnemyRegistry.Enemies;
            EnemyBase best = null;
            float bestDistSq = float.PositiveInfinity;
            Vector3 cam = worldCamera.transform.position;

            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                if (!IsBoss(e)) continue;
                float distSq = ((Vector2)(e.transform.position - cam)).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = e;
                }
            }
            return best;
        }

        private bool IsBoss(EnemyBase enemy)
        {
            if (enemy == null) return false;
            string displayName = enemy.Data != null ? enemy.Data.displayName : null;
            string assetName = enemy.Data != null ? enemy.Data.name : null;
            if (bossNameTokens == null) return false;
            for (int i = 0; i < bossNameTokens.Length; i++)
            {
                string token = bossNameTokens[i];
                if (string.IsNullOrEmpty(token)) continue;
                if (!string.IsNullOrEmpty(displayName)
                    && displayName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (!string.IsNullOrEmpty(assetName)
                    && assetName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the point on a rectangle (centered at <paramref name="center"/>, half-extents
        /// = screen-half minus <paramref name="padding"/>) hit by a ray from center in
        /// direction <paramref name="dir"/>.
        /// </summary>
        private static Vector2 ClampToScreenEdge(Vector2 center, Vector2 dir, float padding)
        {
            float halfW = Mathf.Max(1f, Screen.width * 0.5f - padding);
            float halfH = Mathf.Max(1f, Screen.height * 0.5f - padding);

            // Avoid div-by-zero on axis-aligned directions.
            float absX = Mathf.Abs(dir.x);
            float absY = Mathf.Abs(dir.y);
            float tx = absX > 0.0001f ? halfW / absX : float.PositiveInfinity;
            float ty = absY > 0.0001f ? halfH / absY : float.PositiveInfinity;
            float t = Mathf.Min(tx, ty);

            return center + dir * t;
        }

        private void PlaceArrow(Vector2 screenPos, Vector2 dir)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            // Convert screen-space to local position inside the parent canvas.
            var parentRect = _rt.parent as RectTransform;
            if (parentRect == null)
            {
                _rt.position = screenPos;
            }
            else
            {
                Camera uiCam = (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    ? null
                    : (_canvas != null ? _canvas.worldCamera : null);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCam, out var local))
                {
                    _rt.anchoredPosition = local;
                }
                else
                {
                    _rt.position = screenPos;
                }
            }

            // Rotate so the arrow's "up" axis points at the boss.
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Show()
        {
            if (arrowImage != null && !arrowImage.enabled) arrowImage.enabled = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (arrowImage != null && arrowImage.enabled) arrowImage.enabled = false;
            // Keep the GameObject active so Update() keeps polling for bosses.
        }

        /// <summary>
        /// Build a procedural triangle sprite for the arrow if no image was supplied.
        /// </summary>
        private void EnsureArrowImage()
        {
            if (arrowImage != null)
            {
                arrowImage.color = arrowColor;
                arrowImage.raycastTarget = false;
                if (arrowImage.sprite == null) arrowImage.sprite = CreateTriangleSprite();
                var arrowRt = arrowImage.rectTransform;
                arrowRt.sizeDelta = new Vector2(arrowSize, arrowSize);
                return;
            }

            // Auto-create as a child.
            var go = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            arrowImage = go.GetComponent<Image>();
            arrowImage.color = arrowColor;
            arrowImage.raycastTarget = false;
            arrowImage.sprite = CreateTriangleSprite();

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(arrowSize, arrowSize);
        }

        private static Sprite s_cachedTriangle;

        /// <summary>Generates a small upward-pointing solid-white triangle sprite (cached).</summary>
        private static Sprite CreateTriangleSprite()
        {
            if (s_cachedTriangle != null) return s_cachedTriangle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "MinimapArrow",
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                // Triangle: width tapers linearly from full at y=0 to 0 at y=size-1.
                float t = y / (float)(size - 1);
                float halfWidth = (1f - t) * (size * 0.5f);
                int xMid = size / 2;
                int xMin = Mathf.Max(0, Mathf.RoundToInt(xMid - halfWidth));
                int xMax = Mathf.Min(size - 1, Mathf.RoundToInt(xMid + halfWidth));
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    pixels[row + x] = (x >= xMin && x <= xMax) ? Color.white : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);

            s_cachedTriangle = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            s_cachedTriangle.name = "MinimapArrowSprite";
            s_cachedTriangle.hideFlags = HideFlags.DontSave;
            return s_cachedTriangle;
        }
    }
}
