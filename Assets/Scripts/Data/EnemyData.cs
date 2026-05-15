using UnityEngine;

namespace LoneFighter.Data
{
    [CreateAssetMenu(menuName = "LoneFighter/Enemy", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Grunt";
        public Sprite sprite;

        [Header("Stats")]
        public float maxHealth = 10f;
        public float moveSpeed = 2f;
        public float contactDamage = 5f;
        public float contactCooldown = 0.5f;

        [Header("Rewards")]
        public int xpDrop = 1;
        [Range(0f, 1f)] public float gemDropChance = 1f;

        [Header("Prefab")]
        public GameObject prefab;

        [Header("Visuals (optional, used by EnemyVisualConfig)")]
        [Tooltip("Uniform scale applied to a child SpriteRenderer for quick visual distinction.")]
        public float spriteScale = 1f;
        [Tooltip("Tint applied to a child SpriteRenderer.")]
        public Color tint = Color.white;

        [Header("Ranged (Spitter)")]
        [Tooltip("Projectile prefab fired by ranged archetypes (e.g. Spitter). Must have an EnemyProjectile component.")]
        public GameObject projectilePrefab;
        [Tooltip("Damage dealt by each fired projectile.")]
        public float projectileDamage = 5f;
        [Tooltip("Seconds between shots for ranged archetypes.")]
        public float fireCooldown = 1.5f;

        [Header("AOE (Bomber / Warden)")]
        [Tooltip("Maximum radius of an AOE effect (suicide explosion, shockwave). 0 disables.")]
        public float aoeRadius = 0f;
    }
}
