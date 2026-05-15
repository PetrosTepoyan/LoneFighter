using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyChaseAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;

        private Rigidbody2D _rb;
        private Transform _target;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        private void OnEnable()
        {
            _target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        }

        public void Configure(float speed)
        {
            moveSpeed = speed;
        }

        private void FixedUpdate()
        {
            if (_target == null)
            {
                if (PlayerController.Instance != null) _target = PlayerController.Instance.transform;
                else return;
            }
            Vector2 dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;
        }
    }
}
