using UnityEngine;

namespace LoneFighter.Data
{
    public enum UpgradeKind
    {
        WeaponDamage,
        WeaponCooldown,
        WeaponProjectileSpeed,
        WeaponPierce,
        PlayerMaxHealth,
        PlayerMoveSpeed,
        PlayerMagnetRadius,
        XpGainMultiplier,
        GrantWeapon
    }

    [CreateAssetMenu(menuName = "LoneFighter/Upgrade", fileName = "UpgradeData")]
    public class UpgradeData : ScriptableObject
    {
        [Header("Display")]
        public string displayName = "Bigger Bullets";
        [TextArea] public string description = "+25% weapon damage.";
        public Sprite icon;

        [Header("Effect")]
        public UpgradeKind kind = UpgradeKind.WeaponDamage;
        public float magnitude = 0.25f;
        [Tooltip("Required only when kind == GrantWeapon")]
        public WeaponData weaponToGrant;

        [Header("Constraints")]
        [Range(1, 10)] public int maxStacks = 5;
        [Range(1f, 10f)] public float weight = 1f;
    }
}
