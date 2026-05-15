using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.Weapons
{
    public class WeaponInventory : MonoBehaviour
    {
        [SerializeField] private WeaponData startingWeapon;
        [SerializeField] private GameObject projectileWeaponPrefab;

        private readonly List<WeaponBase> _weapons = new();

        public IReadOnlyList<WeaponBase> Weapons => _weapons;

        private void Start()
        {
            if (startingWeapon != null) GrantWeapon(startingWeapon);
        }

        public WeaponBase GrantWeapon(WeaponData data)
        {
            if (data == null) return null;

            foreach (var existing in _weapons)
            {
                if (existing != null && existing.Data == data) return existing;
            }

            WeaponBase weapon;
            if (projectileWeaponPrefab != null)
            {
                var go = Instantiate(projectileWeaponPrefab, transform);
                weapon = go.GetComponent<WeaponBase>();
                if (weapon == null) weapon = AddResolvedWeaponComponent(go, data);
            }
            else
            {
                var go = new GameObject(data.displayName);
                go.transform.SetParent(transform, false);
                weapon = AddResolvedWeaponComponent(go, data);
            }

            weapon.Initialize(data);
            _weapons.Add(weapon);
            return weapon;
        }

        public void ApplyDamageBonus(float fraction)
        {
            foreach (var w in _weapons) if (w != null) w.AddDamageMultiplier(fraction);
        }

        public void ApplyCooldownReduction(float fraction)
        {
            foreach (var w in _weapons) if (w != null) w.AddCooldownMultiplier(-fraction);
        }

        public void ApplyProjectileSpeedBonus(float fraction)
        {
            foreach (var w in _weapons) if (w != null) w.AddProjectileSpeedMultiplier(fraction);
        }

        public void ApplyPierce(int amount)
        {
            foreach (var w in _weapons) if (w != null) w.AddPierce(amount);
        }

        // Resolves the weapon component named in WeaponData.weaponComponent (within the
        // LoneFighter.Weapons namespace) and AddComponents it. Falls back to ProjectileWeapon
        // if the field is empty or the type can't be resolved / isn't a WeaponBase.
        private static WeaponBase AddResolvedWeaponComponent(GameObject host, WeaponData data)
        {
            string name = data != null ? data.weaponComponent : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                string fullName = name.Contains('.') ? name : ("LoneFighter.Weapons." + name);
                // Try the current assembly first, then the default Unity-managed assembly.
                var type = System.Type.GetType(fullName)
                           ?? System.Type.GetType(fullName + ", Assembly-CSharp");
                if (type != null && typeof(WeaponBase).IsAssignableFrom(type))
                {
                    return host.AddComponent(type) as WeaponBase;
                }
            }
            return host.AddComponent<ProjectileWeapon>();
        }
    }
}
