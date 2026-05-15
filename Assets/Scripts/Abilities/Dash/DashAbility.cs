using System;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Abilities
{
    /// <summary>
    /// Player dash / dodge ability. Cooldown-gated burst velocity in the
    /// current movement direction with a brief i-frame window.
    ///
    /// The Player scene object should host this MonoBehaviour together with
    /// <see cref="DashCharges"/>, <see cref="DashInputHandler"/>,
    /// <see cref="DashTrail"/>, <see cref="DashChargeUi"/> (on a UI Canvas)
    /// and <see cref="DashShockwave"/>. All siblings are optional - this
    /// ability degrades gracefully when any of them are missing.
    ///
    /// Hard rule: we cannot modify <see cref="PlayerHealth"/>, so true
    /// invulnerability is faked two ways:
    ///   1. <see cref="IsAnyInvincible"/> static flag a future
    ///      PlayerHealth patch can read at the start of ApplyDamage.
    ///   2. While dashing, the player's collider layer is swapped to a layer
    ///      that physics-ignores all enemy layers, then restored.
    /// </summary>
    [DisallowMultipleComponent]
    public class DashAbility : MonoBehaviour
    {
        // ---------------- Inspector ----------------

        [Header("Burst")]
        [Tooltip("Burst velocity in units / second applied during the dash.")]
        [SerializeField] private float dashSpeed = 18f;

        [Tooltip("How long the burst velocity is held, in seconds.")]
        [SerializeField] private float dashDuration = 0.18f;

        [Tooltip("Fraction of dash duration that grants i-frames. 1 = entire dash.")]
        [Range(0f, 1f)][SerializeField] private float invincibilityFraction = 1f;

        [Header("Charges")]
        [Tooltip("Seconds to fully regenerate one dash charge.")]
        [SerializeField] private float chargeRegenSeconds = 2.5f;

        [Header("Direction")]
        [Tooltip("If true and player isn't moving, dashes in the last facing direction. " +
                 "If false, a stationary dash request is ignored.")]
        [SerializeField] private bool allowStationaryDash = true;

        [Tooltip("Fallback direction when allowStationaryDash is true and no movement was ever recorded.")]
        [SerializeField] private Vector2 defaultFacing = Vector2.right;

        [Header("Collision (i-frame fake)")]
        [Tooltip("Optional: name of a Physics2D layer that ignores enemy/projectile layers. " +
                 "If left empty, the dash still grants logical i-frames via IsAnyInvincible " +
                 "but doesn't change the collider layer.")]
        [SerializeField] private string dashLayerName = "PlayerDashing";

        // ---------------- Events ----------------

        /// <summary>Raised when a dash actually starts. Argument = world-space direction (unit length).</summary>
        public event Action<Vector2> OnDashStarted;
        /// <summary>Raised when the dash burst window ends and normal control resumes.</summary>
        public event Action OnDashEnded;

        // ---------------- Static i-frame flag ----------------

        private static readonly HashSet<DashAbility> s_invincibleSet = new HashSet<DashAbility>();

        /// <summary>
        /// True if any DashAbility instance is currently inside its i-frame window.
        /// A future patch to <see cref="PlayerHealth.ApplyDamage"/> can early-return
        /// when this is true to grant true invulnerability.
        /// </summary>
        public static bool IsAnyInvincible => s_invincibleSet.Count > 0;

        // ---------------- Runtime state ----------------

        public bool IsDashing { get; private set; }
        public Vector2 DashDirection { get; private set; }
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float ChargeRegenSeconds => chargeRegenSeconds;

        private Rigidbody2D _rb;
        private PlayerController _player;
        private DashCharges _charges;
        private DashTrail _trail;
        private DashShockwave _shockwave;

        private Vector2 _lastMoveDir = Vector2.right;
        private float _dashEndTime;
        private float _ivEndTime;
        private bool _ivActive;

        private int _originalLayer = -1;
        private int _dashLayer = -1;

        private float _cachedMoveSpeed;

        // ---------------- Lifecycle ----------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _player = GetComponent<PlayerController>();
            _charges = GetComponent<DashCharges>();
            _trail = GetComponent<DashTrail>();
            _shockwave = GetComponent<DashShockwave>();

            if (_charges == null) _charges = gameObject.AddComponent<DashCharges>();
            _charges.Configure(this);

            if (!string.IsNullOrEmpty(dashLayerName))
            {
                _dashLayer = LayerMask.NameToLayer(dashLayerName);
                if (_dashLayer < 0)
                {
                    // Layer doesn't exist - that's fine, we just won't swap.
                }
            }
        }

        private void OnDisable()
        {
            // Clean up any lingering i-frame registration if disabled mid-dash.
            ClearInvincible();
            RestoreLayer();
            IsDashing = false;
        }

        private void Update()
        {
            if (_player != null)
            {
                var mv = ReadMoveInput();
                if (mv.sqrMagnitude > 0.0001f) _lastMoveDir = mv.normalized;
            }

            if (IsDashing)
            {
                // Keep applying burst velocity each frame so other systems (knockback,
                // friction) can't kill it mid-dash.
                if (_rb != null) _rb.linearVelocity = DashDirection * dashSpeed;

                if (_ivActive && Time.time >= _ivEndTime)
                {
                    ClearInvincible();
                    RestoreLayer();
                    _ivActive = false;
                }

                if (Time.time >= _dashEndTime)
                {
                    EndDash();
                }
            }
        }

        // ---------------- Public API ----------------

        /// <summary>Returns true if the dash can be triggered right now.</summary>
        public bool CanDash()
        {
            if (IsDashing) return false;
            if (_charges != null && !_charges.HasCharge) return false;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return false;
            return true;
        }

        /// <summary>
        /// Attempts to trigger a dash. Returns true if the dash actually started.
        /// Direction is computed from the current move input; if no input is present
        /// and <c>allowStationaryDash</c> is true, falls back to the last move direction.
        /// </summary>
        public bool TryDash()
        {
            if (!CanDash()) return false;

            Vector2 dir = ResolveDashDirection();
            if (dir.sqrMagnitude < 0.0001f) return false;

            if (_charges != null) _charges.ConsumeCharge();

            BeginDash(dir.normalized);
            return true;
        }

        // ---------------- Internals ----------------

        private Vector2 ResolveDashDirection()
        {
            Vector2 mv = ReadMoveInput();
            if (mv.sqrMagnitude > 0.0001f) return mv;
            if (!allowStationaryDash) return Vector2.zero;
            if (_lastMoveDir.sqrMagnitude > 0.0001f) return _lastMoveDir;
            return defaultFacing.sqrMagnitude > 0.0001f ? defaultFacing : Vector2.right;
        }

        private Vector2 ReadMoveInput()
        {
            // PlayerController exposes MoveSpeed but not raw input. We can read the
            // current rigidbody velocity to infer the most recent move direction.
            if (_rb == null) return Vector2.zero;
            var v = _rb.linearVelocity;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;
        }

        private void BeginDash(Vector2 dir)
        {
            IsDashing = true;
            DashDirection = dir;
            _dashEndTime = Time.time + dashDuration;

            float ivTime = dashDuration * Mathf.Clamp01(invincibilityFraction);
            _ivEndTime = Time.time + ivTime;
            _ivActive = ivTime > 0f;
            if (_ivActive) s_invincibleSet.Add(this);

            SwapLayer();

            if (_player != null)
            {
                _cachedMoveSpeed = _player.MoveSpeed;
                // Force move speed to 0 so PlayerController's FixedUpdate stops
                // overwriting our burst velocity on top of normal movement.
                _player.MoveSpeed = 0f;
            }

            if (_rb != null) _rb.linearVelocity = dir * dashSpeed;

            if (_trail != null) _trail.PlayBurst(dir);

            OnDashStarted?.Invoke(dir);
        }

        private void EndDash()
        {
            IsDashing = false;

            if (_ivActive)
            {
                ClearInvincible();
                _ivActive = false;
            }
            RestoreLayer();

            if (_player != null) _player.MoveSpeed = _cachedMoveSpeed;

            if (_shockwave != null) _shockwave.Emit(transform.position);

            OnDashEnded?.Invoke();
        }

        private void SwapLayer()
        {
            if (_dashLayer < 0) return;
            if (_originalLayer >= 0) return; // already swapped
            _originalLayer = gameObject.layer;
            gameObject.layer = _dashLayer;
        }

        private void RestoreLayer()
        {
            if (_originalLayer < 0) return;
            gameObject.layer = _originalLayer;
            _originalLayer = -1;
        }

        private void ClearInvincible()
        {
            s_invincibleSet.Remove(this);
        }
    }
}
