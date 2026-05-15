using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;
using LoneFighter.Enemies;

namespace LoneFighter.Pickups
{
    /// <summary>
    /// "Screen-nuke" pickup. On collection, deals very heavy damage (or instant-kills) to
    /// every enemy within <see cref="explosionRadius"/> of the player. Triggers a big juice
    /// payload — explosion FX + heavy shake — and is intended to be rare per DESIGN.md.
    /// </summary>
    public class BombPickup : Pickup
    {
        [Tooltip("Pickup trigger radius.")]
        [SerializeField] private float pickupRadius = 0.6f;

        [Tooltip("World-units around the player that the bomb affects.")]
        [SerializeField] private float explosionRadius = 8f;

        [Tooltip("Damage dealt to each enemy inside the radius. Set above any non-boss HP to one-shot.")]
        [SerializeField] private float damage = 9999f;

        private void Update()
        {
            if (_player == null)
            {
                if (PlayerController.Instance == null) return;
                _player = PlayerController.Instance.transform;
            }

            if (Vector2.Distance(transform.position, _player.position) > pickupRadius) return;

            Detonate();
            ReturnToPool();
        }

        private void Detonate()
        {
            Vector3 origin = _player.position;
            float r2 = explosionRadius * explosionRadius;

            // Unity 6 API — no sorting needed, we hit everyone in radius regardless.
            var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                if (((Vector2)enemy.transform.position - (Vector2)origin).sqrMagnitude > r2) continue;
                enemy.ApplyDamage(damage);
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.LevelUpBurst(origin);   // big radial burst, perfect for a bomb
                FxService.Instance.EnemyExplosion(origin); // additional sparks
                FxService.Instance.HeavyShake();
                FxService.Instance.HitStop(0.08f);
            }
        }
    }
}
