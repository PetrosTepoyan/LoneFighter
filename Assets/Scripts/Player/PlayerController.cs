using UnityEngine;
using UnityEngine.InputSystem;
using LoneFighter.Systems;

namespace LoneFighter.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [SerializeField] private float moveSpeed = 5f;

        private Rigidbody2D _rb;
        private Vector2 _moveInput;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Wired up via PlayerInput component (Send Messages or Invoke Unity Events behavior).
        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        public void OnPause(InputValue value)
        {
            if (value.isPressed && GameManager.Instance != null) GameManager.Instance.TogglePause();
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }
            _rb.linearVelocity = _moveInput * moveSpeed;
        }
    }
}
