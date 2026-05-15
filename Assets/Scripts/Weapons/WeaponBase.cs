using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Systems;

namespace LoneFighter.Weapons
{
    public abstract class WeaponBase : MonoBehaviour
    {
        [SerializeField] protected WeaponData data;

        protected float _damageMultiplier = 1f;
        protected float _cooldownMultiplier = 1f;
        protected float _projectileSpeedMultiplier = 1f;
        protected int _bonusPierce = 0;
        protected float _cooldownTimer;

        public WeaponData Data => data;

        public virtual void Initialize(WeaponData newData)
        {
            data = newData;
            _cooldownTimer = 0f;
        }

        public void AddDamageMultiplier(float delta) => _damageMultiplier += delta;
        public void AddCooldownMultiplier(float delta) => _cooldownMultiplier = Mathf.Max(0.05f, _cooldownMultiplier + delta);
        public void AddProjectileSpeedMultiplier(float delta) => _projectileSpeedMultiplier += delta;
        public void AddPierce(int amount) => _bonusPierce += amount;

        protected float CurrentDamage => data != null ? data.damage * _damageMultiplier : 0f;
        protected float CurrentCooldown => data != null ? data.cooldown * _cooldownMultiplier : 0.5f;
        protected float CurrentProjectileSpeed => data != null ? data.projectileSpeed * _projectileSpeedMultiplier : 10f;
        protected int CurrentPierce => data != null ? data.pierce + _bonusPierce : _bonusPierce;

        protected virtual void Update()
        {
            if (data == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                if (TryFire()) _cooldownTimer = CurrentCooldown;
                else _cooldownTimer = 0.1f;
            }
        }

        protected abstract bool TryFire();
    }
}
