using UnityEngine;

namespace LoneFighter.Enemies
{
    /// Reads spriteScale and tint from this enemy's EnemyData and applies them to a SpriteRenderer
    /// so every archetype can share one placeholder sprite but still look distinct in-scene.
    ///
    /// Attach to the same GameObject as EnemyBase. By default it finds the first SpriteRenderer
    /// in children and scales `spriteRoot` (which defaults to that renderer's transform).
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyVisualConfig : MonoBehaviour
    {
        [Tooltip("Renderer that receives the tint. Defaults to the first SpriteRenderer in children.")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Transform that receives the scale. Defaults to spriteRenderer.transform.")]
        [SerializeField] private Transform spriteRoot;

        private EnemyBase _enemy;
        private Vector3 _baseScale = Vector3.one;
        private bool _captured;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRoot == null && spriteRenderer != null) spriteRoot = spriteRenderer.transform;
            if (spriteRoot != null)
            {
                _baseScale = spriteRoot.localScale;
                _captured = true;
            }
        }

        private void OnEnable()
        {
            Apply();
        }

        /// Call after EnemyBase.Configure to refresh visuals when the SO swaps at runtime.
        public void Apply()
        {
            if (_enemy == null || _enemy.Data == null) return;
            var data = _enemy.Data;

            if (spriteRenderer != null) spriteRenderer.color = data.tint;

            if (spriteRoot != null)
            {
                float s = Mathf.Max(0.0001f, data.spriteScale);
                Vector3 baseScale = _captured ? _baseScale : Vector3.one;
                spriteRoot.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
            }
        }
    }
}
