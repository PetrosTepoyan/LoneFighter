using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.Enemies.Elites
{
    /// <summary>
    /// Promotes an existing <see cref="EnemyBase"/> to "Elite" status:
    ///
    ///  * Visual: scales the root transform 1.4x and tints every child
    ///    <see cref="SpriteRenderer"/> with a gold trim (originals are cached so
    ///    OnDisable restores them — important because <c>PoolService</c> recycles enemy
    ///    GameObjects, and we must not bleed gold tint into a regular grunt that gets the
    ///    same instance later).
    ///  * Stats: bumps the linked <see cref="EnemyHealth"/>'s Max HP to 3x and refills it.
    ///    Contact damage is scaled 1.5x via a cloned <see cref="EnemyData"/> ScriptableObject
    ///    pushed through <see cref="EnemyBase.Configure"/>.  See the LIMITATION block in
    ///    <see cref="ApplyContactDamageScale"/> for why we don't mutate the shared SO directly
    ///    and what we cannot scale because of the EnemyBase public API.
    ///  * Companions: spawns an <see cref="EliteAura"/> particle child and attaches an
    ///    <see cref="EliteDropOnDeath"/> drop trigger.
    ///  * Announce: pings <see cref="EliteSpawnDirector"/> so the banner + indicator wake up.
    ///
    /// Added at runtime by <see cref="EliteSpawnDirector"/> via <c>AddComponent</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class EliteModifier : MonoBehaviour
    {
        /// <summary>Uniform scale multiplier applied to the host transform.</summary>
        public const float ScaleMultiplier = 1.4f;

        /// <summary>HP multiplier applied through <c>EnemyHealth.Initialize</c>.</summary>
        public const float HealthMultiplier = 3f;

        /// <summary>Contact damage multiplier baked into the cloned <c>EnemyData</c>.</summary>
        public const float ContactDamageMultiplier = 1.5f;

        /// <summary>Default gold trim. Multiplied with each renderer's original color.</summary>
        public static readonly Color GoldTint = new Color(1f, 0.85f, 0.35f, 1f);

        [Header("Drops")]
        [Tooltip("If set, an EliteDropOnDeath listener is added on enable with this table.")]
        [SerializeField] private EliteDropTable dropTable;

        [Header("Visual")]
        [Tooltip("Optional aura prefab spawned as a child. If null, an EliteAura is attached on-the-fly with a default ParticleSystem.")]
        [SerializeField] private GameObject auraPrefab;

        private EnemyBase _enemy;
        private EnemyHealth _health;

        // Restoration state — required because enemies are pooled.
        private Vector3 _baseLocalScale;
        private readonly List<SpriteRenderer> _tintedRenderers = new();
        private readonly List<Color> _originalColors = new();
        private EnemyData _clonedData;            // SO clone we own and destroy
        private GameObject _auraInstance;
        private EliteDropOnDeath _dropHook;
        private bool _applied;

        /// <summary>Public accessor for the host so the spawn director / indicator can dedupe.</summary>
        public EnemyBase Host => _enemy;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _health = GetComponent<EnemyHealth>();
            _baseLocalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_applied) return;
            if (_enemy == null) _enemy = GetComponent<EnemyBase>();
            if (_health == null) _health = GetComponent<EnemyHealth>();
            if (_enemy == null || _health == null)
            {
                Debug.LogWarning("[EliteModifier] Missing EnemyBase/EnemyHealth — cannot promote.", this);
                enabled = false;
                return;
            }

            // We capture the *current* local scale so re-promoting a pooled enemy still
            // works (the scale will already be back to base because OnDisable restored it).
            _baseLocalScale = transform.localScale;
            transform.localScale = _baseLocalScale * ScaleMultiplier;

            ApplyGoldTint();
            ApplyHealthScale();
            ApplyContactDamageScale();
            EnsureDropHook();
            SpawnAura();

            EliteSpawnDirector.NotifyEliteAppeared(this);
            _applied = true;
        }

        private void OnDisable()
        {
            // Restore everything we touched. Pool reuse: same GameObject can become a
            // normal grunt next time it's spawned — leaving gold tint / 3x HP would be
            // a real bug.
            if (!_applied)
            {
                CleanupExtras();
                return;
            }
            _applied = false;

            transform.localScale = _baseLocalScale;
            RestoreOriginalColors();
            CleanupExtras();
        }

        // ----- visuals ------------------------------------------------------------

        private void ApplyGoldTint()
        {
            _tintedRenderers.Clear();
            _originalColors.Clear();
            var renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                _tintedRenderers.Add(r);
                _originalColors.Add(r.color);
                // Multiplicative blend keeps any data-driven base tint visible.
                r.color = new Color(
                    r.color.r * GoldTint.r,
                    r.color.g * GoldTint.g,
                    r.color.b * GoldTint.b,
                    r.color.a * GoldTint.a);
            }
        }

        private void RestoreOriginalColors()
        {
            for (int i = 0; i < _tintedRenderers.Count; i++)
            {
                var r = _tintedRenderers[i];
                if (r != null) r.color = _originalColors[i];
            }
            _tintedRenderers.Clear();
            _originalColors.Clear();
        }

        // ----- stats --------------------------------------------------------------

        private void ApplyHealthScale()
        {
            // EnemyHealth.Max is private-set. Initialize is the public path; it also refills
            // Current to Max and clears the dead flag.
            float scaledMax = (_health.Max > 0f ? _health.Max : 10f) * HealthMultiplier;
            _health.Initialize(scaledMax);
        }

        /// <summary>
        /// Contact-damage scaling. LIMITATION: <see cref="EnemyBase"/> reads
        /// <c>data.contactDamage</c> from the shared <see cref="EnemyData"/> SO every
        /// collision frame, and exposes no per-instance damage override. Mutating the
        /// SO in place would scale *every* enemy of that type globally for the rest
        /// of the run.
        ///
        /// Workaround: we clone the SO via <see cref="Object.Instantiate"/>, scale
        /// <c>contactDamage</c> on the clone, and push it through
        /// <see cref="EnemyBase.Configure"/>. This works for contact damage, but has
        /// a known side-effect: <c>Configure</c> also overwrites the private
        /// <c>xpGemPrefab</c> field with whatever we pass (we pass <c>null</c> because
        /// there is no public getter for the current value). That's acceptable for
        /// elites because their drops are handled by <see cref="EliteDropTable"/> via
        /// <see cref="EliteDropOnDeath"/>; the gem branch in <c>EnemyBase.HandleDeath</c>
        /// is short-circuited by the null check.
        ///
        /// Other contact attributes on the SO (cooldown, AOE radius, ranged projectile
        /// damage, etc.) are scaled 1.0x intentionally — the elite is meant to be a
        /// fat / hard-hitting variant, not a fundamentally different archetype.
        /// </summary>
        private void ApplyContactDamageScale()
        {
            var original = _enemy.Data;
            if (original == null)
            {
                // No SO to clone — skip silently; elite is still a beefier health sponge.
                return;
            }

            _clonedData = Object.Instantiate(original);
            _clonedData.name = original.name + " (Elite)";
            _clonedData.contactDamage = original.contactDamage * ContactDamageMultiplier;
            _clonedData.projectileDamage = original.projectileDamage * ContactDamageMultiplier;

            // Note: passing null gem prefab is intentional — see method-level remarks.
            _enemy.Configure(_clonedData, null);

            // Configure also calls EnemyHealth.Initialize(clone.maxHealth), which would
            // overwrite the 3x scale we set above. Re-apply.
            ApplyHealthScale();
        }

        // ----- companions ---------------------------------------------------------

        private void EnsureDropHook()
        {
            if (dropTable == null) return;
            _dropHook = gameObject.AddComponent<EliteDropOnDeath>();
            _dropHook.Configure(dropTable);
        }

        private void SpawnAura()
        {
            if (auraPrefab != null)
            {
                _auraInstance = Object.Instantiate(auraPrefab, transform);
                _auraInstance.transform.localPosition = Vector3.zero;
                _auraInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _auraInstance = new GameObject("EliteAura");
                _auraInstance.transform.SetParent(transform, false);
                _auraInstance.AddComponent<EliteAura>();
            }
        }

        private void CleanupExtras()
        {
            if (_auraInstance != null)
            {
                Object.Destroy(_auraInstance);
                _auraInstance = null;
            }
            if (_dropHook != null)
            {
                Object.Destroy(_dropHook);
                _dropHook = null;
            }
            if (_clonedData != null)
            {
                Object.Destroy(_clonedData);
                _clonedData = null;
            }
        }

        // ----- public config (called from EliteSpawnDirector before enable) -------

        /// <summary>
        /// Sets the drop table + optional aura prefab. Safe to call before the component
        /// has been enabled for the first time.
        /// </summary>
        public void Configure(EliteDropTable table, GameObject overrideAuraPrefab = null)
        {
            dropTable = table;
            if (overrideAuraPrefab != null) auraPrefab = overrideAuraPrefab;
        }
    }
}
