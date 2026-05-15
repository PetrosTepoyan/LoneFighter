using UnityEngine;
using LoneFighter.Pickups;

namespace LoneFighter.Player
{
    [RequireComponent(typeof(Collider2D))]
    public class XPCollector : MonoBehaviour
    {
        [Tooltip("Distance at which gems start magnetizing toward the player.")]
        [SerializeField] private float magnetRadius = 2.5f;
        [SerializeField] private float pickupRadius = 0.5f;
        [SerializeField] private float magnetForce = 18f;

        public float MagnetRadius
        {
            get => magnetRadius;
            set => magnetRadius = Mathf.Max(0f, value);
        }

        public float PickupRadius => pickupRadius;
        public float MagnetForce => magnetForce;

        private PlayerLevel _level;

        private void Awake()
        {
            _level = GetComponentInParent<PlayerLevel>();
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        public void NotifyPickup(XPGem gem)
        {
            if (gem == null || _level == null) return;
            _level.AddXp(gem.Value);
        }
    }
}
