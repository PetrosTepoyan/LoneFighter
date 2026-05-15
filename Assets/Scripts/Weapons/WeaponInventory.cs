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
                if (weapon == null) weapon = go.AddComponent<ProjectileWeapon>();
            }
            else
            {
                var go = new GameObject(data.displayName);
                go.transform.SetParent(transform, false);
                weapon = go.AddComponent<ProjectileWeapon>();
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
    }
}
