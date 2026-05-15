using System;
using UnityEngine;

namespace LoneFighter.Abilities
{
    /// <summary>
    /// Tracks dash charge state. Default 1 charge, upgradable via <see cref="SetMaxCharges"/>.
    /// Charges regenerate over real time at <c>DashAbility.ChargeRegenSeconds</c> per charge.
    ///
    /// Upgrade hooks:
    ///   - Extra charges: <see cref="SetMaxCharges"/>
    ///   - Faster cooldown: <see cref="SetRegenScale"/> (multiplier; 0.5 = twice as fast)
    /// </summary>
    [DisallowMultipleComponent]
    public class DashCharges : MonoBehaviour
    {
        [SerializeField] private int maxCharges = 1;

        /// <summary>Fired whenever <see cref="Current"/>, <see cref="Max"/>, or progress changes.</summary>
        public event Action<DashCharges> OnChanged;

        public int Max => maxCharges;
        public int Current { get; private set; }

        /// <summary>0..1 fill progress toward the next charge. 1 when full.</summary>
        public float RechargeProgress01 { get; private set; }

        public bool HasCharge => Current > 0;
        public bool IsFull => Current >= maxCharges;

        private DashAbility _ability;
        private float _regenTimer;
        private float _regenScale = 1f;

        internal void Configure(DashAbility ability)
        {
            _ability = ability;
            Current = Mathf.Clamp(Current == 0 ? maxCharges : Current, 0, maxCharges);
            RechargeProgress01 = IsFull ? 1f : 0f;
            OnChanged?.Invoke(this);
        }

        private void Awake()
        {
            if (Current == 0 && maxCharges > 0) Current = maxCharges;
        }

        private void Update()
        {
            if (IsFull)
            {
                if (RechargeProgress01 != 1f)
                {
                    RechargeProgress01 = 1f;
                    OnChanged?.Invoke(this);
                }
                return;
            }

            float duration = (_ability != null ? _ability.ChargeRegenSeconds : 2.5f) * Mathf.Max(0.01f, _regenScale);
            _regenTimer += Time.deltaTime;

            float prog = Mathf.Clamp01(_regenTimer / duration);
            if (!Mathf.Approximately(prog, RechargeProgress01))
            {
                RechargeProgress01 = prog;
                OnChanged?.Invoke(this);
            }

            if (_regenTimer >= duration)
            {
                _regenTimer = 0f;
                Current = Mathf.Min(maxCharges, Current + 1);
                RechargeProgress01 = IsFull ? 1f : 0f;
                OnChanged?.Invoke(this);
            }
        }

        /// <summary>Consumes one charge. No-op if none available.</summary>
        public bool ConsumeCharge()
        {
            if (!HasCharge) return false;
            bool wasFull = IsFull;
            Current--;
            if (wasFull) _regenTimer = 0f; // start the regen clock fresh
            RechargeProgress01 = IsFull ? 1f : 0f;
            OnChanged?.Invoke(this);
            return true;
        }

        /// <summary>Refills all charges (e.g. on level-up).</summary>
        public void Refill()
        {
            Current = maxCharges;
            _regenTimer = 0f;
            RechargeProgress01 = 1f;
            OnChanged?.Invoke(this);
        }

        /// <summary>
        /// Upgrade hook: change max charges. If <paramref name="grantNew"/> is true,
        /// any new charges added by this call are immediately granted.
        /// </summary>
        public void SetMaxCharges(int value, bool grantNew = true)
        {
            int newMax = Mathf.Max(1, value);
            int delta = newMax - maxCharges;
            maxCharges = newMax;
            if (delta > 0 && grantNew) Current = Mathf.Min(maxCharges, Current + delta);
            Current = Mathf.Clamp(Current, 0, maxCharges);
            RechargeProgress01 = IsFull ? 1f : 0f;
            OnChanged?.Invoke(this);
        }

        /// <summary>
        /// Upgrade hook: multiplicative regen speed. 1 = default; 0.5 = twice as fast;
        /// 2 = half as fast.
        /// </summary>
        public void SetRegenScale(float scale)
        {
            _regenScale = Mathf.Max(0.01f, scale);
        }
    }
}
