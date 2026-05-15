using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Effects.Decals
{
    /// <summary>
    /// Top-level glue between gameplay and the decal system. Owns ALL knowledge of when
    /// a decal should be spawned — no other file in the project is touched.
    ///
    /// Strategy:
    /// - Per frame, snapshot <see cref="EnemyRegistry.Enemies"/> and remember each live
    ///   enemy's last position.
    /// - Next frame, any enemy that vanished from the registry but was alive last frame
    ///   counts as a "death" — we drop a <see cref="BloodSplatter"/> at the cached position.
    ///   (In practice the only way an EnemyBase leaves the registry mid-game is via
    ///   HandleDeath → ReturnToPool → OnDisable, so this is functionally equivalent
    ///   to a death event without us subscribing to or modifying anything.)
    /// - For bombers, when an enemy is discovered for the first time we try
    ///   <c>TryGetComponent&lt;BomberBehavior&gt;</c> on its GameObject; if present we
    ///   remember its blast radius and, on its death, spawn an additional
    ///   <see cref="ScorchMark"/>. If absent, we silently skip — graceful no-op.
    ///
    /// Performance: we keep two dictionaries keyed by EnemyBase. Lookups are amortized O(1).
    /// Dictionaries are pre-warmed and never shrink, so per-frame allocations stay near zero
    /// once the high-water mark is reached.
    /// </summary>
    [DisallowMultipleComponent]
    public class DecalAutoSubscriber : MonoBehaviour
    {
        [Header("Behavior")]
        [Tooltip("If false the subscriber does nothing — useful for A/B testing perf.")]
        [SerializeField] private bool enabledAtRuntime = true;
        [Tooltip("When the player dies / game stops, freeze new spawns. Existing decals continue fading.")]
        [SerializeField] private bool respectGameState = true;
        [Tooltip("Probability of dropping a blood splatter on enemy death. 1 = every kill.")]
        [Range(0f, 1f)] [SerializeField] private float bloodChance = 1f;
        [Tooltip("Probability of dropping a scorch mark when a bomber dies. 1 = every bomber.")]
        [Range(0f, 1f)] [SerializeField] private float bomberScorchChance = 1f;
        [Tooltip("Scale multiplier applied on top of a bomber's reported radius when drawing the scorch.")]
        [SerializeField] private float scorchRadiusFactor = 0.55f;

        /// <summary>Cached last-known transform position per live enemy.</summary>
        private readonly Dictionary<EnemyBase, Vector3> _lastPos = new();
        /// <summary>Cached blast radius for enemies that carry a BomberBehavior (0 if not a bomber).</summary>
        private readonly Dictionary<EnemyBase, float>   _bomberRadius = new();
        /// <summary>Scratch HashSet for the "still alive this frame" set. Cleared & reused — no GC.</summary>
        private readonly HashSet<EnemyBase> _seenThisFrame = new();
        /// <summary>Scratch list for collecting deaths before mutating _lastPos. Avoids "modify while iterating".</summary>
        private readonly List<EnemyBase> _deathsScratch = new();

        // ---- Lifecycle ----

        private void Awake()
        {
            // Touch the manager so it exists before any decal call.
            DecalManager.GetOrCreate();
        }

        private void OnDisable()
        {
            _lastPos.Clear();
            _bomberRadius.Clear();
            _seenThisFrame.Clear();
            _deathsScratch.Clear();
        }

        private void Update()
        {
            if (!enabledAtRuntime) return;

            // Optional gameplay-state gate. If the GameManager isn't around (e.g. menu scene),
            // we still process — the registry will just be empty.
            if (respectGameState && GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                // Don't process while paused / game over — but keep our cached state so we
                // don't burst-spawn decals on resume.
                return;
            }

            ScanRegistry();
            DetectDeathsAndSpawn();
        }

        // ---- Polling ----

        /// <summary>
        /// Walk the registry once. For each currently-alive enemy:
        /// - If we've never seen it, do the BomberBehavior TryGetComponent probe.
        /// - Update its last-known position.
        /// - Mark it as seen this frame.
        /// </summary>
        private void ScanRegistry()
        {
            _seenThisFrame.Clear();
            var list = EnemyRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null) continue;
                _seenThisFrame.Add(e);
                if (!_lastPos.ContainsKey(e))
                {
                    // First-seen — probe for bomber. Graceful no-op if the component isn't there.
                    if (e.TryGetComponent<BomberBehavior>(out var bomber))
                    {
                        // We don't have a direct radius getter on BomberBehavior; the actual
                        // effective radius lives behind its private `Radius` property. Best
                        // available proxy without modifying that file is the EnemyData.aoeRadius.
                        float r = (e.Data != null && e.Data.aoeRadius > 0f) ? e.Data.aoeRadius : 2.5f;
                        _bomberRadius[e] = r;
                    }
                }
                _lastPos[e] = e.transform.position;
            }
        }

        /// <summary>
        /// Anything in <see cref="_lastPos"/> that wasn't in <see cref="_seenThisFrame"/> counts
        /// as a death — spawn the appropriate decal at its cached last position and forget it.
        /// </summary>
        private void DetectDeathsAndSpawn()
        {
            _deathsScratch.Clear();
            foreach (var kv in _lastPos)
            {
                if (!_seenThisFrame.Contains(kv.Key)) _deathsScratch.Add(kv.Key);
            }
            for (int i = 0; i < _deathsScratch.Count; i++)
            {
                var dead = _deathsScratch[i];
                Vector3 pos = _lastPos[dead];
                _lastPos.Remove(dead);

                // Bomber scorch first (so blood overlays on top — looks better).
                if (_bomberRadius.TryGetValue(dead, out float radius))
                {
                    _bomberRadius.Remove(dead);
                    if (Random.value < bomberScorchChance)
                    {
                        ScorchMark.Spawn(pos, Mathf.Max(0.5f, radius * scorchRadiusFactor));
                    }
                }

                if (Random.value < bloodChance)
                {
                    BloodSplatter.Spawn(pos);
                }
            }
        }
    }
}
