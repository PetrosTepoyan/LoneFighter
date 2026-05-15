using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Pickups
{
    /// <summary>
    /// "Mass attract" pickup. On collection, every active <see cref="XPGem"/> in the scene is
    /// flagged to magnetize regardless of distance, so the player slurps up the field.
    ///
    /// Implementation: we set each gem's local position toward the player (close enough to
    /// trigger the XPGem's own pickup-radius check next Update tick). This keeps the actual
    /// pickup/score path identical to the normal flow — no special accounting needed.
    /// </summary>
    public class MagnetPickup : Pickup
    {
        [SerializeField] private float pickupRadius = 0.6f;

        [Tooltip("Distance from the player at which collected gems are placed. Should be well inside any XPCollector.PickupRadius.")]
        [SerializeField] private float slurpDropDistance = 0.05f;

        private void Update()
        {
            if (_player == null)
            {
                if (PlayerController.Instance == null) return;
                _player = PlayerController.Instance.transform;
            }

            if (Vector2.Distance(transform.position, _player.position) > pickupRadius) return;

            // Unity 6 API. SortMode.None is fastest — we don't care about order.
            var gems = Object.FindObjectsByType<XPGem>(FindObjectsSortMode.None);
            Vector3 pp = _player.position;
            foreach (var gem in gems)
            {
                if (gem == null || !gem.isActiveAndEnabled) continue;
                // Snap the gem essentially on top of the player; its own Update tick
                // will register the pickup with XPCollector on the very next frame.
                Vector3 dir = (gem.transform.position - pp);
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    gem.transform.position = pp + dir * slurpDropDistance;
                }
                else
                {
                    gem.transform.position = pp;
                }
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.XpPickup(_player.position);
                FxService.Instance.LightShake();
            }

            ReturnToPool();
        }
    }
}
