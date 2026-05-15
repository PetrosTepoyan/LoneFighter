using UnityEngine;

namespace LoneFighter.Data
{
    [CreateAssetMenu(menuName = "LoneFighter/Weapon", fileName = "WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Pistol";
        public Sprite icon;

        [Header("Combat")]
        public float damage = 10f;
        public float cooldown = 0.5f;
        public float projectileSpeed = 12f;
        public float projectileLifetime = 2f;
        public int pierce = 0;
        public float range = 10f;

        [Header("Prefabs")]
        public GameObject projectilePrefab;
    }
}
