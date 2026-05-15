using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Weapons;

namespace LoneFighter.Effects.PlayerAura
{
    /// <summary>
    /// Spawns a child SpriteRenderer aura behind the player for each currently
    /// active weapon, using the matching <see cref="WeaponAuraProfile"/> resolved
    /// through a <see cref="WeaponAuraRegistry"/>.
    ///
    /// <para>WeaponInventory does not expose change events, so this controller
    /// polls the inventory list at a fixed cadence (default 1 Hz) and diffs the
    /// active set, instantiating / destroying aura children as needed. Polling at
    /// 1 Hz is cheap and the diff itself is allocation-free for a stable set.</para>
    ///
    /// <para>Each aura uses an additive-friendly HDR-tinted SpriteRenderer and is
    /// driven by a sinusoidal pulse for scale + alpha. The aura sits behind the
    /// player by virtue of the profile's sorting layer / order configuration.</para>
    /// </summary>
    public class PlayerAuraController : MonoBehaviour
    {
        [Header("Registry")]
        [Tooltip("Maps WeaponData display names to aura profiles. Required for any aura to spawn.")]
        [SerializeField] private WeaponAuraRegistry registry;

        [Header("Inventory")]
        [Tooltip("WeaponInventory to mirror. If null, will look on this GameObject and parents at Start.")]
        [SerializeField] private WeaponInventory weaponInventory;

        [Header("Polling")]
        [Tooltip("Seconds between weapon-list diff polls. The list is small so 1 Hz is plenty.")]
        [SerializeField, Min(0.1f)] private float pollIntervalSeconds = 1f;

        [Header("Level Source")]
        [Tooltip("Optional PlayerLevel used as a generic 'power' input for intensity-over-level. " +
                 "If null, looks on this GameObject and parents at Start. " +
                 "If still null, level is treated as 1.")]
        [SerializeField] private PlayerLevel playerLevel;

        [Tooltip("Clamps the level fed into WeaponAuraProfile.GetIntensityForLevel / GetScaleForLevel.")]
        [SerializeField, Min(1)] private int maxLevelForAura = 20;

        [Header("Render")]
        [Tooltip("Optional explicit parent transform for aura children. Defaults to this transform.")]
        [SerializeField] private Transform auraRoot;

        // Live mapping from active weapon -> spawned aura instance.
        private readonly Dictionary<WeaponBase, AuraInstance> _live = new();

        // Scratch buffer reused each poll to detect removals without allocations.
        private readonly List<WeaponBase> _seenScratch = new();

        private float _pollTimer;

        private class AuraInstance
        {
            public GameObject go;
            public SpriteRenderer sr;
            public WeaponAuraProfile profile;
            // Random phase offset so multiple auras don't pulse in lockstep.
            public float phase;
        }

        private void Awake()
        {
            if (auraRoot == null) auraRoot = transform;
        }

        private void Start()
        {
            if (weaponInventory == null) weaponInventory = GetComponentInParent<WeaponInventory>();
            if (playerLevel == null) playerLevel = GetComponentInParent<PlayerLevel>();
            // Force an immediate sync so the starting weapon's aura shows up on frame 1.
            _pollTimer = pollIntervalSeconds;
        }

        private void OnDisable()
        {
            // Tear everything down so OnEnable starts clean (registry / profile swap friendly).
            foreach (var kv in _live)
            {
                if (kv.Value != null && kv.Value.go != null) Destroy(kv.Value.go);
            }
            _live.Clear();
        }

        private void Update()
        {
            _pollTimer += Time.deltaTime;
            if (_pollTimer >= pollIntervalSeconds)
            {
                _pollTimer = 0f;
                SyncWithInventory();
            }
            DriveAuras();
        }

        private void SyncWithInventory()
        {
            if (weaponInventory == null || registry == null) return;

            _seenScratch.Clear();
            var weapons = weaponInventory.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                if (w == null || w.Data == null) continue;
                _seenScratch.Add(w);

                if (!_live.ContainsKey(w))
                {
                    var profile = registry.Resolve(w.Data.displayName);
                    if (profile == null || profile.sprite == null) continue;
                    _live[w] = SpawnAura(w, profile);
                }
            }

            // Remove auras whose weapon has gone away (or been destroyed).
            // Iterate over a snapshot of the keys so we can safely mutate the dict.
            if (_live.Count > 0)
            {
                // Reuse list via simple foreach + pending list to avoid LINQ alloc.
                List<WeaponBase> pendingRemoval = null;
                foreach (var kv in _live)
                {
                    if (kv.Key == null || !_seenScratch.Contains(kv.Key))
                    {
                        pendingRemoval ??= new List<WeaponBase>(2);
                        pendingRemoval.Add(kv.Key);
                    }
                }
                if (pendingRemoval != null)
                {
                    for (int i = 0; i < pendingRemoval.Count; i++)
                    {
                        if (_live.TryGetValue(pendingRemoval[i], out var inst))
                        {
                            if (inst != null && inst.go != null) Destroy(inst.go);
                        }
                        _live.Remove(pendingRemoval[i]);
                    }
                }
            }
        }

        private AuraInstance SpawnAura(WeaponBase weapon, WeaponAuraProfile profile)
        {
            var go = new GameObject($"Aura_{(weapon.Data != null ? weapon.Data.displayName : "Weapon")}");
            go.transform.SetParent(auraRoot, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = profile.sprite;
            sr.color = profile.color;
            if (!string.IsNullOrEmpty(profile.sortingLayerName))
            {
                sr.sortingLayerName = profile.sortingLayerName;
            }
            sr.sortingOrder = profile.sortingOrder;

            return new AuraInstance
            {
                go = go,
                sr = sr,
                profile = profile,
                phase = Random.value * Mathf.PI * 2f,
            };
        }

        private int CurrentLevel()
        {
            int level = playerLevel != null ? playerLevel.Level : 1;
            return Mathf.Clamp(level, 1, maxLevelForAura);
        }

        private void DriveAuras()
        {
            if (_live.Count == 0) return;
            int level = CurrentLevel();
            float t = Time.time;

            foreach (var kv in _live)
            {
                var inst = kv.Value;
                if (inst == null || inst.go == null || inst.profile == null) continue;

                var profile = inst.profile;
                float baseScale = profile.GetScaleForLevel(level);
                float baseIntensity = profile.GetIntensityForLevel(level);

                float wave = profile.pulseHz > 0f
                    ? Mathf.Sin(t * profile.pulseHz * Mathf.PI * 2f + inst.phase)
                    : 0f; // wave in [-1, 1]

                float scaleMod = 1f + wave * profile.pulseScaleAmplitude;
                float alphaMod = 1f + wave * profile.pulseAlphaAmplitude;

                inst.go.transform.localScale = Vector3.one * (baseScale * scaleMod);

                var c = profile.color;
                c.a = Mathf.Max(0f, profile.color.a * baseIntensity * alphaMod);
                inst.sr.color = c;
            }
        }
    }
}
