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
    }
}
