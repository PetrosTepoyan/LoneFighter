using UnityEngine;

namespace LoneFighter.Enemies.Advanced
{
    /// Marker component placed on the "shard" prefab spawned by SplitterEnemy on death.
    ///
    /// Purely a type-tag: its presence means "this enemy is already a shard — do not split
    /// again on death." SplitterEnemy.shardData.prefab MUST reference a prefab that has this
    /// component AND does NOT have a SplitterEnemy component. Two separate prefabs is the
    /// correct setup; sharing one prefab would create an infinite split chain.
    ///
    /// We keep it as a real MonoBehaviour (rather than a tag string) so it shows up in the
    /// Inspector and can be used as a type filter via GetComponent / TryGetComponent — handy
    /// for analytics or boss-room rules that may want to count shards separately.
    [DisallowMultipleComponent]
    public class SplitterSmall : MonoBehaviour
    {
        [Tooltip("Optional cosmetic hint: shards spawned by SplitterEnemy are typically tinted darker.")]
        [SerializeField] private bool dimOnSpawn = true;

        [Tooltip("Color multiplier applied to a child SpriteRenderer's color on enable when dimOnSpawn is true.")]
        [SerializeField] private Color dimMultiplier = new Color(0.75f, 0.75f, 0.75f, 1f);

        [SerializeField] private SpriteRenderer spriteRenderer;
        private Color _baseColor;
        private bool _captured;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
                _captured = true;
            }
        }

        private void OnEnable()
        {
            if (!dimOnSpawn || !_captured || spriteRenderer == null) return;
            spriteRenderer.color = new Color(
                _baseColor.r * dimMultiplier.r,
                _baseColor.g * dimMultiplier.g,
                _baseColor.b * dimMultiplier.b,
                _baseColor.a * dimMultiplier.a);
        }

        private void OnDisable()
        {
            // Restore so the pool hands the next consumer a clean color.
            if (_captured && spriteRenderer != null) spriteRenderer.color = _baseColor;
        }
    }
}
