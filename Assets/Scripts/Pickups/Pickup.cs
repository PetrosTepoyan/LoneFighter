using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Pickups
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class Pickup : MonoBehaviour
    {
        protected Transform _player;

        protected virtual void OnEnable()
        {
            _player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        protected void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
