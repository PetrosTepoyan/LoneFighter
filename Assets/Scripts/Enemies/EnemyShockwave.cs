using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Enemies
{
    /// Pooled expanding-ring AOE. Starts at radius 0, grows to maxRadius over expandDuration.
    /// Damages any PlayerHealth caught inside the current radius once per shockwave instance,
    /// then returns to the pool.
    ///
    /// Setup notes:
    /// - The prefab should have a child SpriteRenderer (or Sprite Shape) for the visual ring.
    ///   The visual is uniformly scaled to match the current radius via the local transform of
    ///   `visualRoot` (defaults to this transform when unset).
    /// - The player layer can be configured on the prefab; if left unset (mask == 0) we
    ///   fall back to scanning all colliders and filtering by PlayerHealth.
    public class EnemyShockwave : MonoBehaviour
    {
        [Header("Geometry")]
        [SerializeField] private float maxRadius = 3.5f;
        [SerializeField] private float expandDuration = 0.6f;

        [Header("Damage")]
        [SerializeField] private float damage = 12f;
        [Tooltip("Layer mask used by Physics2D.OverlapCircleAll. Set to the Player layer for performance. If 0, the wave scans everything and filters by PlayerHealth.")]
        [SerializeField] private LayerMask playerLayerMask;

        [Header("Visual")]
        [Tooltip("Transform that gets uniformly scaled to follow the current radius. Defaults to this transform.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Visual scale that corresponds to a radius of 1 unit. Tune to your ring sprite size.")]
        [SerializeField] private float visualScalePerUnit = 2f;

        private float _elapsed;
        private readonly HashSet<PlayerHealth> _alreadyHit = new();
        private static readonly Collider2D[] _overlapBuffer = new Collider2D[16];

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
        }

        public void Configure(float radius, float duration, float damageAmount)
        {
            if (radius > 0f) maxRadius = radius;
            if (duration > 0f) expandDuration = duration;
            if (damageAmount > 0f) damage = damageAmount;
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _alreadyHit.Clear();
            ApplyVisualScale(0f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = expandDuration > 0f ? Mathf.Clamp01(_elapsed / expandDuration) : 1f;
            float currentRadius = Mathf.Lerp(0f, maxRadius, t);

            ApplyVisualScale(currentRadius);
            ApplyDamage(currentRadius);

            if (t >= 1f) ReturnToPool();
        }

        private void ApplyVisualScale(float radius)
        {
            if (visualRoot == null) return;
            float s = Mathf.Max(0.0001f, radius * visualScalePerUnit);
            visualRoot.localScale = new Vector3(s, s, 1f);
        }

        private void ApplyDamage(float radius)
        {
            if (radius <= 0f) return;
            Vector2 origin = transform.position;

            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            if (playerLayerMask.value != 0)
            {
                filter.useLayerMask = true;
                filter.layerMask = playerLayerMask;
            }
            int count = Physics2D.OverlapCircle(origin, radius, filter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                var health = col.GetComponentInParent<PlayerHealth>();
                if (health == null) continue;
                if (_alreadyHit.Contains(health)) continue;
                _alreadyHit.Add(health);
                health.ApplyDamage(damage);
            }
        }

        private void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
