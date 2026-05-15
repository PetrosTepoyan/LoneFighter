using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Polish;
using LoneFighter.Systems;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Central facade for tiered explosions. External callers fire one method —
    /// <see cref="Detonate"/> — and the service dispatches across every visual,
    /// audio-haptic and physics layer in parallel:
    ///
    ///   1. Pooled <see cref="ExplosionFlash"/> (white-hot core)
    ///   2. Pooled <see cref="ExplosionShockwave"/> (additive expanding ring)
    ///   3. Pooled <see cref="ExplosionDebris"/> chunks fanned radially
    ///   4. Pooled <see cref="ExplosionSmoke"/> lingering puff
    ///   5. <see cref="ScreenFlashOverlay"/> UI flash (Large+ only)
    ///   6. <see cref="FxService.HeavyShake"/> 0-3 times based on tier
    ///   7. <see cref="FxService.HitStop"/> scaled by tier
    ///   8. <see cref="HapticsService.Pulse"/> at the profile's intensity
    ///   9. Area damage via <see cref="Physics2D.OverlapCircle"/> when
    ///      <paramref name="damage"/> &gt; 0 and the profile has a damageRadius
    ///
    /// Each layer fails gracefully when its prefab / instance is missing. Drop in
    /// a single <c>ExplosionService</c> GameObject into <c>Game.unity</c>, generate
    /// the profiles via <c>LoneFighter -> FX -> Generate Explosion Profiles</c>,
    /// and you're cooking with grease.
    /// </summary>
    public class ExplosionService : MonoBehaviour
    {
        public static ExplosionService Instance { get; private set; }

        [Header("Profiles (one per tier)")]
        [Tooltip("Per-tier ScriptableObject profiles. Use the LoneFighter -> FX -> Generate Explosion Profiles menu to populate.")]
        [SerializeField] private ExplosionProfile[] profiles = new ExplosionProfile[0];

        [Header("Pooled Prefabs")]
        [Tooltip("Prefab carrying an ExplosionFlash component. If null, instances are created at runtime from a bare GameObject.")]
        [SerializeField] private GameObject flashPrefab;
        [Tooltip("Prefab carrying an ExplosionShockwave component.")]
        [SerializeField] private GameObject shockwavePrefab;
        [Tooltip("Prefab carrying an ExplosionDebris component.")]
        [SerializeField] private GameObject debrisPrefab;
        [Tooltip("Prefab carrying an ExplosionSmoke component.")]
        [SerializeField] private GameObject smokePrefab;

        [Header("Damage")]
        [Tooltip("Layer mask used when scanning for damageable colliders. Set to Enemy + Player if you want explosions to hurt both sides; leave 0 to scan all layers and filter by component.")]
        [SerializeField] private LayerMask damageLayerMask;

        [Tooltip("If true, damage applied to a discovered collider is scaled by linear distance falloff from the blast center (full at center, 0 at edge).")]
        [SerializeField] private bool useDistanceFalloff = true;

        [Header("Diagnostics")]
        [SerializeField] private bool logMissingTier = true;

        // Auto-created fallback prefabs so the service still detonates without authoring assets.
        private GameObject _runtimeFlashPrefab;
        private GameObject _runtimeShockwavePrefab;
        private GameObject _runtimeDebrisPrefab;
        private GameObject _runtimeSmokePrefab;

        // Tier -> profile lookup. Built lazily so swapping the profiles array at runtime
        // (e.g. for difficulty tuning) is supported by a single call to RebuildLookup().
        private readonly Dictionary<ExplosionTier, ExplosionProfile> _byTier = new();
        // Reused overlap buffer keeps zero GC per detonation.
        private static readonly Collider2D[] _overlapBuffer = new Collider2D[64];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            RebuildLookup();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Recomputes the tier -> profile mapping. Call after swapping <see cref="profiles"/> at runtime.</summary>
        public void RebuildLookup()
        {
            _byTier.Clear();
            if (profiles == null) return;
            for (int i = 0; i < profiles.Length; i++)
            {
                var p = profiles[i];
                if (p == null) continue;
                _byTier[p.tier] = p;
            }
        }

        /// <summary>
        /// Resolve the profile for a tier. Returns null if no profile is wired; callers
        /// should tolerate that (Detonate already does).
        /// </summary>
        public ExplosionProfile GetProfile(ExplosionTier tier)
        {
            if (_byTier.Count == 0 && profiles != null && profiles.Length > 0) RebuildLookup();
            _byTier.TryGetValue(tier, out var profile);
            return profile;
        }

        /// <summary>
        /// Main entry point. Fires every layer for the given tier at <paramref name="worldPos"/>.
        /// If <paramref name="damage"/> is positive AND the resolved profile has a damageRadius &gt; 0,
        /// a Physics2D area scan applies damage to overlapping <c>EnemyHealth</c> / <c>PlayerHealth</c>
        /// components.
        /// </summary>
        public void Detonate(Vector3 worldPos, ExplosionTier tier, float damage = 0f)
        {
            var profile = GetProfile(tier);
            if (profile == null)
            {
                if (logMissingTier) Debug.LogWarning($"[ExplosionService] No profile wired for tier {tier}. Detonation skipped.");
                return;
            }

            SpawnVisuals(worldPos, profile);
            ApplyScreenAndCameraFx(profile);

            if (damage > 0f && profile.damageRadius > 0f)
            {
                ApplyAreaDamage(worldPos, profile, damage);
            }
        }

        // ----------------------------------------------------------------------
        // Visual dispatch
        // ----------------------------------------------------------------------

        private void SpawnVisuals(Vector3 worldPos, ExplosionProfile profile)
        {
            // 1. Core flash
            var flashGo = SpawnPooled(GetOrBuildFlashPrefab(), worldPos);
            if (flashGo != null)
            {
                var flash = flashGo.GetComponent<ExplosionFlash>() ?? flashGo.AddComponent<ExplosionFlash>();
                flash.Play(profile);
            }

            // 2. Shockwave
            var ringGo = SpawnPooled(GetOrBuildShockwavePrefab(), worldPos);
            if (ringGo != null)
            {
                var ring = ringGo.GetComponent<ExplosionShockwave>() ?? ringGo.AddComponent<ExplosionShockwave>();
                ring.Play(profile);
            }

            // 3. Debris fan
            if (profile.debrisCount > 0)
            {
                var debrisPrefab = GetOrBuildDebrisPrefab();
                int count = profile.debrisCount;
                // Even angular spacing with a slight random jitter so the ring doesn't look mechanical.
                float baseAngle = Random.Range(0f, 360f);
                for (int i = 0; i < count; i++)
                {
                    float ang = baseAngle + (360f / count) * i + Random.Range(-8f, 8f);
                    var dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                    var dgo = SpawnPooled(debrisPrefab, worldPos);
                    if (dgo == null) continue;
                    var d = dgo.GetComponent<ExplosionDebris>() ?? dgo.AddComponent<ExplosionDebris>();
                    d.Launch(dir, profile);
                }
            }

            // 4. Smoke
            if (profile.spawnSmoke)
            {
                var smokeGo = SpawnPooled(GetOrBuildSmokePrefab(), worldPos);
                if (smokeGo != null)
                {
                    var smoke = smokeGo.GetComponent<ExplosionSmoke>() ?? smokeGo.AddComponent<ExplosionSmoke>();
                    smoke.Play(profile);
                }
            }
        }

        private GameObject SpawnPooled(GameObject prefab, Vector3 worldPos)
        {
            if (prefab == null) return null;
            if (PoolService.Instance != null)
            {
                return PoolService.Instance.Get(prefab, worldPos, Quaternion.identity);
            }
            return Instantiate(prefab, worldPos, Quaternion.identity);
        }

        // ----------------------------------------------------------------------
        // Screen + camera + haptics
        // ----------------------------------------------------------------------

        private void ApplyScreenAndCameraFx(ExplosionProfile profile)
        {
            // Screen flash overlay (Large / Mega / Nuke)
            if (profile.screenFlashAlpha > 0.001f)
            {
                var overlay = ScreenFlashOverlay.Instance != null
                    ? ScreenFlashOverlay.Instance
                    : ScreenFlashOverlay.GetOrCreate();
                if (overlay != null)
                {
                    var col = profile.screenFlashColor;
                    col.a = profile.screenFlashAlpha;
                    overlay.Flash(col, profile.screenFlashDuration);
                }
            }

            // Camera shake — 0..3 calls, lets Nuke physically rattle the frame.
            if (FxService.Instance != null && profile.shakeCount > 0)
            {
                for (int i = 0; i < profile.shakeCount; i++)
                {
                    FxService.Instance.HeavyShake();
                }
            }

            // Hit-stop
            if (FxService.Instance != null && profile.hitStopSeconds > 0f)
            {
                FxService.Instance.HitStop(profile.hitStopSeconds);
            }

            // Haptics
            if (profile.enableHaptics && HapticsService.Instance != null)
            {
                HapticsService.Instance.Pulse(profile.hapticIntensity);
            }
        }

        // ----------------------------------------------------------------------
        // Area damage
        // ----------------------------------------------------------------------

        private void ApplyAreaDamage(Vector3 worldPos, ExplosionProfile profile, float baseDamage)
        {
            float radius = profile.damageRadius;
            float scaled = baseDamage * profile.damageMultiplier;
            if (scaled <= 0f || radius <= 0f) return;

            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            if (damageLayerMask.value != 0)
            {
                filter.useLayerMask = true;
                filter.layerMask = damageLayerMask;
            }

            int count = Physics2D.OverlapCircle(worldPos, radius, filter, _overlapBuffer);
            if (count <= 0) return;

            // Each transform is hit at most once per detonation, even if it has multiple colliders.
            var seen = new HashSet<Transform>();
            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;

                // Falloff: linear from full damage at center to 0 at the radius edge.
                float dist = ((Vector2)col.bounds.ClosestPoint(worldPos) - (Vector2)worldPos).magnitude;
                float falloff = useDistanceFalloff
                    ? Mathf.Clamp01(1f - dist / Mathf.Max(0.0001f, radius))
                    : 1f;
                if (falloff <= 0f) continue;
                float dmg = scaled * falloff;

                // Apply damage to whichever known health interface this collider exposes.
                var t = col.transform.root;
                if (seen.Contains(t)) continue;
                seen.Add(t);

                var enemy = col.GetComponentInParent<Enemies.EnemyHealth>();
                if (enemy != null)
                {
                    enemy.ApplyDamage(dmg);
                    continue;
                }

                var enemyBase = col.GetComponentInParent<Enemies.EnemyBase>();
                if (enemyBase != null)
                {
                    enemyBase.ApplyDamage(dmg);
                    continue;
                }

                var player = col.GetComponentInParent<Player.PlayerHealth>();
                if (player != null)
                {
                    player.ApplyDamage(dmg);
                }
            }
        }

        // ----------------------------------------------------------------------
        // Lazy prefab fallbacks
        // ----------------------------------------------------------------------

        private GameObject GetOrBuildFlashPrefab()
        {
            if (flashPrefab != null) return flashPrefab;
            if (_runtimeFlashPrefab != null) return _runtimeFlashPrefab;
            _runtimeFlashPrefab = BuildPrimitive<ExplosionFlash>("Explosion_Flash_Runtime");
            return _runtimeFlashPrefab;
        }

        private GameObject GetOrBuildShockwavePrefab()
        {
            if (shockwavePrefab != null) return shockwavePrefab;
            if (_runtimeShockwavePrefab != null) return _runtimeShockwavePrefab;
            _runtimeShockwavePrefab = BuildPrimitive<ExplosionShockwave>("Explosion_Shockwave_Runtime");
            return _runtimeShockwavePrefab;
        }

        private GameObject GetOrBuildDebrisPrefab()
        {
            if (debrisPrefab != null) return debrisPrefab;
            if (_runtimeDebrisPrefab != null) return _runtimeDebrisPrefab;
            _runtimeDebrisPrefab = BuildPrimitive<ExplosionDebris>("Explosion_Debris_Runtime");
            return _runtimeDebrisPrefab;
        }

        private GameObject GetOrBuildSmokePrefab()
        {
            if (smokePrefab != null) return smokePrefab;
            if (_runtimeSmokePrefab != null) return _runtimeSmokePrefab;
            _runtimeSmokePrefab = BuildPrimitive<ExplosionSmoke>("Explosion_Smoke_Runtime");
            return _runtimeSmokePrefab;
        }

        /// <summary>
        /// Build a runtime "prefab" (a disabled GameObject parented to the service) that
        /// the <see cref="PoolService"/> can clone. Each visual layer is added by type so
        /// the component's <c>EnsureRenderer()</c> path bootstraps the sprite.
        /// </summary>
        private GameObject BuildPrimitive<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.SetActive(false);
            go.AddComponent<T>();
            return go;
        }
    }
}
