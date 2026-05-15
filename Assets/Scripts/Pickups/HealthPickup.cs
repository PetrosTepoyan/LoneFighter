using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Pickups
{
    /// <summary>
    /// On collection, heals the player by <see cref="healAmount"/>. Rare drop intended to give
    /// the player a small lifeline in the 2:30–4:00 valley described in DESIGN.md without
    /// trivializing the run.
    /// </summary>
    public class HealthPickup : Pickup
    {
        [Tooltip("Flat HP restored when picked up.")]
        [SerializeField] private float healAmount = 25f;

        [Tooltip("Distance at which the pickup is consumed (mirrors XPGem.pickupRadius style).")]
        [SerializeField] private float pickupRadius = 0.6f;

        public float HealAmount
        {
            get => healAmount;
            set => healAmount = Mathf.Max(0f, value);
        }

        private void Update()
        {
            if (_player == null)
            {
                if (PlayerController.Instance == null) return;
                _player = PlayerController.Instance.transform;
            }

            if (Vector2.Distance(transform.position, _player.position) > pickupRadius) return;

            var ph = _player.GetComponent<PlayerHealth>();
            if (ph == null) ph = _player.GetComponentInChildren<PlayerHealth>();
            if (ph != null) ph.Heal(healAmount);

            if (FxService.Instance != null)
            {
                // Reuse the XP pickup pop — it's already a tasty green/cyan burst.
                FxService.Instance.XpPickup(transform.position);
                FxService.Instance.LightShake();
            }

            ReturnToPool();
        }
    }
}
