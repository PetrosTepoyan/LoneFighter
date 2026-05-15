using TMPro;
using UnityEngine;
using LoneFighter.Weapons;

namespace LoneFighter.UI.WeaponHud
{
    /// <summary>
    /// Bottom-right "+N" badge on a weapon slot. The number is a coarse "upgrade tier"
    /// derived from <see cref="WeaponBase"/>'s protected runtime multipliers (read
    /// via <see cref="WeaponBaseExtensions"/>).
    ///
    /// WeaponBase doesn't expose an upgrade-count; <see cref="WeaponInventory"/>
    /// applies deltas like <c>+0.1</c> damage, <c>+0.1</c> projectile speed, etc.
    /// We approximate the tier as the sum of 10%-steps across the multipliers plus
    /// any bonus pierce. That gives a number that monotonically tracks "how often
    /// has this weapon been upgraded?" without us having to listen to the
    /// upgrade-service stream (which we're not allowed to modify).
    ///
    /// Tier 0 → badge hidden (no upgrades yet).
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponLevelBadge : MonoBehaviour
    {
        [Tooltip("Text element to write '+N' into. Hidden when tier == 0.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Optional background graphic to hide along with the label at tier 0.")]
        [SerializeField] private GameObject background;

        [Tooltip("Each 0.1 step in a non-pierce multiplier counts as 1 upgrade.")]
        [SerializeField] private float perStep = 0.1f;

        private int _lastTier = -1;

        public void Bind(WeaponBase weapon) => Refresh(weapon);

        public void Refresh(WeaponBase weapon)
        {
            int tier = ComputeTier(weapon);
            if (tier == _lastTier) return;
            _lastTier = tier;

            bool show = tier > 0;
            if (label != null)
            {
                label.gameObject.SetActive(show);
                if (show) label.text = "+" + tier.ToString();
            }
            if (background != null) background.SetActive(show);
        }

        private int ComputeTier(WeaponBase weapon)
        {
            if (weapon == null || perStep <= 0f) return 0;

            float dmg = weapon.GetDamageMultiplier();           // baseline 1
            float cdMult = weapon.GetCooldownMultiplier();      // baseline 1
            float spd = weapon.GetProjectileSpeedMultiplier();  // baseline 1
            int pierce = weapon.GetBonusPierce();               // baseline 0

            int steps = 0;
            steps += Mathf.Max(0, Mathf.RoundToInt((dmg - 1f) / perStep));
            // CooldownReduction is applied as a NEGATIVE delta, so a "faster" weapon has cdMult < 1.
            steps += Mathf.Max(0, Mathf.RoundToInt((1f - cdMult) / perStep));
            steps += Mathf.Max(0, Mathf.RoundToInt((spd - 1f) / perStep));
            steps += Mathf.Max(0, pierce);
            return steps;
        }
    }
}
