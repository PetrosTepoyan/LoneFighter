using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.World
{
    /// <summary>
    /// Lingering poison patch on the ground. Designed to be spawned by Spitter enemies
    /// (see BALANCE.md) via <see cref="PoolService"/>: it auto-returns to the pool after
    /// <see cref="lifetime"/> seconds so it never leaks.
    ///
    /// Place a <see cref="CircleCollider2D"/> on the same GameObject and tweak its radius
    /// to control the AoE size. The Hazard base handles damage cadence.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class PoisonPuddle : Hazard
    {
        [Header("Lifetime")]
        [Tooltip("Seconds the puddle lingers before returning to the pool. Spitter design assumes ~5s.")]
        [SerializeField] private float lifetime = 5f;

        [Header("Visual")]
        [Tooltip("Optional sprite renderer to fade out over the last 25% of life. Auto-found if null.")]
        [SerializeField] private SpriteRenderer fadeRenderer;

        private float _spawnTime;
        private Color _baseColor;

        protected override void OnEnable()
        {
            base.OnEnable();
            _spawnTime = Time.time;
            if (fadeRenderer == null) fadeRenderer = GetComponent<SpriteRenderer>();
            if (fadeRenderer != null) _baseColor = fadeRenderer.color;
        }

        private void Update()
        {
            float alive = Time.time - _spawnTime;
            if (fadeRenderer != null && lifetime > 0f)
            {
                float t = Mathf.InverseLerp(lifetime * 0.75f, lifetime, alive);
                var c = _baseColor;
                c.a = _baseColor.a * (1f - t);
                fadeRenderer.color = c;
            }
            if (alive >= lifetime) Despawn();
        }

        /// <summary>Configure the puddle when spawned through the pool.</summary>
        public void Configure(float radius, float damage, float interval, float life)
        {
            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.radius = Mathf.Max(0.01f, radius);
            damagePerTick = Mathf.Max(0f, damage);
            tickInterval = Mathf.Max(0.05f, interval);
            lifetime = Mathf.Max(0.1f, life);
        }

        private void Despawn()
        {
            if (fadeRenderer != null) fadeRenderer.color = _baseColor; // restore before pool re-use
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
