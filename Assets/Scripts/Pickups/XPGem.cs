using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Pickups
{
    public class XPGem : Pickup
    {
        [SerializeField] private int value = 1;

        public int Value => value;

        public void SetValue(int newValue) => value = Mathf.Max(1, newValue);

        private void Update()
        {
            if (_player == null)
            {
                if (PlayerController.Instance == null) return;
                _player = PlayerController.Instance.transform;
            }

            var collector = _player.GetComponentInChildren<XPCollector>();
            if (collector == null) return;

            float dist = Vector2.Distance(transform.position, _player.position);
            if (dist <= collector.PickupRadius)
            {
                collector.NotifyPickup(this);
                if (FxService.Instance != null) FxService.Instance.XpPickup(transform.position);
                ReturnToPool();
                return;
            }

            if (dist <= collector.MagnetRadius)
            {
                Vector2 dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
                transform.position += (Vector3)(dir * collector.MagnetForce * Time.deltaTime);
            }
        }
    }
}
