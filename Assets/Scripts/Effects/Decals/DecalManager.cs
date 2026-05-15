using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Effects.Decals
{
    /// <summary>
    /// Singleton MonoBehaviour that owns a fixed-size ring of pooled SpriteRenderers used
    /// as floor decals (blood splatters, scorch marks, bullet holes). Recycles oldest-first
    /// once the cap is reached.
    ///
    /// Design notes:
    /// - Hard cap (default 50) is the only memory you spend on decals. Any further spawn
    ///   evicts the oldest slot — no allocation, no GC, no Destroy/Instantiate at runtime
    ///   after warm-up.
    /// - All decal renderers share a sorting layer and use <see cref="sortingOrder"/> well
    ///   below the gameplay layer so they always read as "on the floor".
    /// - The manager auto-bootstraps from a static accessor — drop one component in the
    ///   scene or just let <see cref="Spawn"/> create one on first call. We deliberately
    ///   do NOT modify any existing scene/prefab/script.
    /// - Fade-out is linear over <see cref="DecalRequest.fadeSeconds"/>. We update alphas in
    ///   a tight Update loop (no coroutines, no allocations).
    /// </summary>
    [DisallowMultipleComponent]
    public class DecalManager : MonoBehaviour
    {
        // ---------- Singleton ----------

        public static DecalManager Instance { get; private set; }

        /// <summary>
        /// Returns the existing instance or lazily creates one in a hidden GameObject.
        /// Safe to call from any system — failing silently (returning null) is never the
        /// behavior; the ring always exists once asked for.
        /// </summary>
        public static DecalManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            // Search first — someone may have placed one in the scene that hasn't run Awake yet.
            var existing = FindAnyObjectByType<DecalManager>(FindObjectsInactive.Include);
            if (existing != null) { existing.EnsureInitialized(); return existing; }
            var go = new GameObject("DecalManager (auto)");
            var mgr = go.AddComponent<DecalManager>();
            mgr.EnsureInitialized();
            return mgr;
        }

        // ---------- Inspector ----------

        [Header("Capacity")]
        [Tooltip("Maximum simultaneous floor decals. Oldest is recycled when exceeded.")]
        [SerializeField] private int maxDecals = 50;

        [Header("Rendering")]
        [Tooltip("Sorting layer name for floor decals. Leave 'Default' if you don't use a custom layer.")]
        [SerializeField] private string sortingLayerName = "Default";
        [Tooltip("Sorting order — must be below enemies. Negative values keep decals on the floor.")]
        [SerializeField] private int sortingOrder = -50;
        [Tooltip("Z offset applied to every decal so they sit slightly behind gameplay sprites without sorting fights.")]
        [SerializeField] private float zOffset = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool logEvictions = false;

        // ---------- Pool storage ----------

        /// <summary>
        /// One slot in the ring. The GameObject + SpriteRenderer are allocated once during
        /// <see cref="EnsureInitialized"/> and then reused forever.
        /// </summary>
        private sealed class Slot
        {
            public GameObject go;
            public SpriteRenderer renderer;
            public float timeRemaining;
            public float lifetime;
            public Color baseTint;     // RGB only; alpha is recomputed every frame from fade progress.
            public float maxAlpha;
            public bool active;
        }

        private Slot[] _slots;
        // Ring write head — next index to overwrite. Always advances modulo maxDecals.
        private int _ringHead;
        private bool _initialized;

        // ---------- Lifecycle ----------

        private void Awake()
        {
            // Standard singleton — first one wins, duplicates self-destruct.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            if (maxDecals <= 0) maxDecals = 1; // safety: a cap of 0 would deadlock the ring math.
            _slots = new Slot[maxDecals];
            for (int i = 0; i < maxDecals; i++)
            {
                _slots[i] = CreateSlot(i);
            }
            _ringHead = 0;
            _initialized = true;
        }

        private Slot CreateSlot(int index)
        {
            var go = new GameObject($"Decal_{index:00}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            sr.sortingOrder = sortingOrder;
            // No shadow casting / receiving — these are SpriteRenderers but be explicit anyway.
            sr.receiveShadows = false;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Slot
            {
                go = go,
                renderer = sr,
                timeRemaining = 0f,
                lifetime = 0f,
                baseTint = Color.white,
                maxAlpha = 1f,
                active = false,
            };
        }

        // ---------- Spawn API ----------

        /// <summary>
        /// Spawn a floor decal. If the ring is full the OLDEST decal is recycled (FIFO).
        /// Silently no-ops if <see cref="DecalRequest.sprite"/> is null.
        /// </summary>
        public void Spawn(DecalRequest req)
        {
            if (!_initialized) EnsureInitialized();
            if (req.sprite == null) return;
            if (req.fadeSeconds <= 0f) req.fadeSeconds = 0.01f; // avoid div-by-zero in fade math.
            if (req.maxAlpha <= 0f) req.maxAlpha = 1f;
            if (req.scale <= 0f) req.scale = 1f;

            int idx = _ringHead;
            _ringHead = (_ringHead + 1) % _slots.Length;

            var slot = _slots[idx];
            if (slot.active && logEvictions)
            {
                Debug.Log($"[DecalManager] Evicting decal slot {idx} (ring full at {maxDecals}).");
            }

            // Compose final transform — preserve incoming z but apply our z offset so floor decals
            // sit just behind active gameplay sprites (camera looks down -Z by default in 2D).
            var pos = req.position;
            pos.z += zOffset;
            slot.go.transform.SetPositionAndRotation(pos, req.rotation);
            slot.go.transform.localScale = new Vector3(req.scale, req.scale, 1f);

            slot.renderer.sprite = req.sprite;
            // Capture RGB; alpha drives over the lifetime in Update.
            slot.baseTint = new Color(req.tint.r, req.tint.g, req.tint.b, 1f);
            slot.maxAlpha = Mathf.Clamp01(req.maxAlpha);
            slot.lifetime = req.fadeSeconds;
            slot.timeRemaining = req.fadeSeconds;
            slot.renderer.color = new Color(slot.baseTint.r, slot.baseTint.g, slot.baseTint.b, slot.maxAlpha);
            slot.renderer.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            slot.renderer.sortingOrder = sortingOrder;
            slot.go.SetActive(true);
            slot.active = true;
        }

        /// <summary>Convenience overload — pass the fields you actually care about.</summary>
        public void Spawn(Vector3 position, Sprite sprite, Color tint, float fadeSeconds)
            => Spawn(new DecalRequest(position, sprite, tint, fadeSeconds));

        /// <summary>Forcibly clear every decal. Useful between runs.</summary>
        public void ClearAll()
        {
            if (!_initialized) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].active = false;
                _slots[i].timeRemaining = 0f;
                if (_slots[i].go != null) _slots[i].go.SetActive(false);
            }
            _ringHead = 0;
        }

        /// <summary>Active count — useful for stats/perf overlay.</summary>
        public int ActiveCount
        {
            get
            {
                if (_slots == null) return 0;
                int n = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null && _slots[i].active) n++;
                return n;
            }
        }

        public int Capacity => _slots != null ? _slots.Length : maxDecals;

        // ---------- Tick ----------

        private void Update()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;
            // We use Time.deltaTime (scaled) so decals freeze with hit-stop / pause —
            // which is the natural-feeling behavior for floor stains.
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s == null || !s.active) continue;
                s.timeRemaining -= dt;
                if (s.timeRemaining <= 0f)
                {
                    s.active = false;
                    s.go.SetActive(false);
                    continue;
                }
                // Linear fade from maxAlpha → 0. Cheap; mobile-friendly.
                float t = s.lifetime > 0f ? (s.timeRemaining / s.lifetime) : 0f;
                float a = s.maxAlpha * t;
                var c = s.baseTint;
                c.a = a;
                s.renderer.color = c;
            }
        }
    }
}
