using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Bosses.Roster
{
    /// MirrorBoss — at 50% HP the boss splits, conjuring a clone of itself. The fight is
    /// only over when BOTH copies are dead. When one dies, the survivor goes into an
    /// "enraged" state via <see cref="MirrorLink"/>.
    ///
    /// HP model: We cannot share a single HP pool across two GameObjects through Unity's
    /// component system without a custom ScriptableObject pointer. To keep the contract
    /// simple, the clone spawns with half of the original's max HP. The pair fight is
    /// balanced as "two independent bars" - see README.md for the rationale and trade-offs.
    ///
    /// Composition:
    /// - Attach this to an EnemyBase prefab. Use the same prefab reference for cloning
    ///   when authored on a prefab (assign it to <see cref="clonePrefab"/>). When the boss
    ///   is itself a scene instance with no prefab, we fall back to <see cref="Instantiate"/>
    ///   of the current GameObject.
    /// - Optional: assign a <see cref="MirrorSplitVfx"/> prefab to play a flourish on split.
    [RequireComponent(typeof(EnemyBase))]
    [RequireComponent(typeof(EnemyHealth))]
    [DisallowMultipleComponent]
    public class MirrorBoss : MonoBehaviour
    {
        [Header("Splitting")]
        [Tooltip("HP fraction at which the split occurs (default 50%).")]
        [Range(0.05f, 0.95f)] [SerializeField] private float splitHpThreshold = 0.5f;
        [Tooltip("Prefab spawned for the clone. Should be a MirrorBoss prefab (typically THIS prefab).")]
        [SerializeField] private GameObject clonePrefab;
        [Tooltip("How far from the original the clone appears.")]
        [SerializeField] private float cloneSpawnOffset = 1.5f;

        [Header("Clone Appearance")]
        [Tooltip("Tint applied to the clone's SpriteRenderer. Default: pale blue.")]
        [SerializeField] private Color cloneTint = new Color(0.65f, 0.85f, 1f, 0.7f);
        [Tooltip("If true, the clone counts as a clone and will not split again. (Prevents recursive splits.)")]
        [SerializeField] private bool isClone = false;

        [Header("Split Vfx")]
        [Tooltip("Optional MirrorSplitVfx prefab played on the split moment.")]
        [SerializeField] private GameObject splitVfxPrefab;

        [Header("Enrage")]
        [Tooltip("Set automatically by MirrorLink when the partner dies. Read-only at edit-time.")]
        [SerializeField] private bool enraged;

        // Cached components.
        private EnemyBase _enemy;
        private EnemyHealth _health;
        private EnemyChaseAI _chaseAi;
        private SpriteRenderer _spriteRenderer;
        private MirrorLink _link;

        // Stored baselines so enrage can be cleanly undone on disable / reuse.
        private float _baseContactDamage;
        private float _baseMoveSpeed;
        private bool _splitPerformed;

        public bool IsClone => isClone;
        public bool IsEnraged => enraged;
        public MirrorLink Link => _link;

        // Public-facing hook for the link to register itself.
        public void OnLinkAttached(MirrorLink link)
        {
            _link = link;
        }

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _health = GetComponent<EnemyHealth>();
            _chaseAi = GetComponent<EnemyChaseAI>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _splitPerformed = false;
            enraged = false;
            // Snapshot baselines from the active EnemyData (we never mutate the SO).
            if (_enemy != null && _enemy.Data != null)
            {
                _baseContactDamage = _enemy.Data.contactDamage;
                _baseMoveSpeed = _enemy.Data.moveSpeed;
            }
            // If this is a clone instance, apply the tint immediately.
            if (isClone) ApplyCloneTint();
        }

        private void OnDisable()
        {
            // Restore baselines on the SO if we monkey-patched them via Configure.
            // (Configure overrides EnemyBase's per-instance state; the SO itself is untouched.)
            enraged = false;
            _link = null;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (_splitPerformed || isClone) return;
            if (_health == null || _health.Max <= 0f) return;

            float hpFrac = _health.Current / _health.Max;
            if (hpFrac <= splitHpThreshold)
            {
                PerformSplit();
            }
        }

        // ---- split flow -------------------------------------------------------

        private void PerformSplit()
        {
            _splitPerformed = true;
            GameObject cloneGo = SpawnClone();
            if (cloneGo == null) return;

            var cloneBoss = cloneGo.GetComponent<MirrorBoss>();
            if (cloneBoss == null) return;

            // Mark the clone as a clone (no recursive splits).
            cloneBoss.isClone = true;
            cloneBoss.ApplyCloneTint();

            // Halve the clone's HP relative to the parent's max.
            var cloneHealth = cloneGo.GetComponent<EnemyHealth>();
            if (cloneHealth != null) cloneHealth.Initialize(Mathf.Max(1f, _health.Max * 0.5f));

            // Pair the twins under a fresh MirrorLink so both deaths gate the fight end.
            GameObject linkHost = new GameObject("MirrorLink");
            linkHost.transform.SetParent(transform.parent, false);
            var link = linkHost.AddComponent<MirrorLink>();
            link.Bind(this, cloneBoss);

            PlaySplitVfx(cloneGo);
        }

        private GameObject SpawnClone()
        {
            Vector3 dir = (Random.value < 0.5f ? Vector3.left : Vector3.right) +
                          (Random.value < 0.5f ? Vector3.up * 0.6f : Vector3.down * 0.6f);
            Vector3 pos = transform.position + dir.normalized * cloneSpawnOffset;

            GameObject go;
            if (clonePrefab != null && PoolService.Instance != null)
            {
                go = PoolService.Instance.Get(clonePrefab, pos, transform.rotation);
            }
            else if (clonePrefab != null)
            {
                go = Instantiate(clonePrefab, pos, transform.rotation);
            }
            else
            {
                // No prefab assigned - duplicate the live GameObject in-place. Note this
                // produces a scene clone (no pooling, no shared prefab linkage).
                go = Instantiate(gameObject, pos, transform.rotation);
            }
            if (go == null) return null;

            // Make sure the clone enemy carries the same EnemyData (so its stats line up).
            var cloneEnemy = go.GetComponent<EnemyBase>();
            if (cloneEnemy != null && _enemy != null && _enemy.Data != null)
            {
                cloneEnemy.Configure(_enemy.Data, null);
            }

            return go;
        }

        private void PlaySplitVfx(GameObject cloneGo)
        {
            if (splitVfxPrefab == null) return;
            GameObject vfxGo = PoolService.Instance != null
                ? PoolService.Instance.Get(splitVfxPrefab, transform.position, Quaternion.identity)
                : Instantiate(splitVfxPrefab, transform.position, Quaternion.identity);
            if (vfxGo == null) return;
            var vfx = vfxGo.GetComponent<MirrorSplitVfx>();
            if (vfx != null) vfx.Play(transform.position, transform, cloneGo != null ? cloneGo.transform : null);
        }

        // ---- enrage -----------------------------------------------------------

        /// Called by MirrorLink when this boss is the last twin standing.
        /// We multiply contact damage and move speed via EnemyBase.Configure, which writes a
        /// per-instance copy into EnemyHealth/EnemyChaseAI without touching the EnemyData SO.
        public void ApplyEnrageMultipliers(float damageMult, float speedMult)
        {
            if (enraged) return;
            enraged = true;

            if (_enemy != null && _enemy.Data != null)
            {
                // Build an instance-scoped EnemyData so we never mutate the shared asset.
                // Caveat: EnemyBase.Configure also rewires the xpGemPrefab; passing null here
                // means the enraged survivor will not drop a gem on death. This is an
                // intentional, documented trade-off (see README) - the alternative would be
                // to reflect into EnemyBase's private gem field, which we explicitly avoid.
                var data = _enemy.Data;
                var instanceData = ScriptableObject.Instantiate(data);
                instanceData.contactDamage = _baseContactDamage * damageMult;
                instanceData.moveSpeed = _baseMoveSpeed * speedMult;
                _enemy.Configure(instanceData, null);
            }
            else if (_chaseAi != null)
            {
                _chaseAi.Configure(_baseMoveSpeed * speedMult);
            }

            // Visual cue: shift toward a stronger red overlay on the survivor.
            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.r = Mathf.Clamp01(c.r + 0.25f);
                _spriteRenderer.color = c;
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.HeavyShake();
            }
        }

        // ---- visuals ----------------------------------------------------------

        private void ApplyCloneTint()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null) return;
            _spriteRenderer.color = cloneTint;
        }
    }
}
