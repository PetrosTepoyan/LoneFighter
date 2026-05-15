using UnityEngine;
using UnityEngine.InputSystem;

namespace LoneFighter.Abilities
{
    /// <summary>
    /// Hooks the new Input System for the dash. The Dash action is defined
    /// programmatically here so we don't have to edit InputActions.inputactions.
    ///
    /// Bindings:
    ///   - Keyboard: Space
    ///   - Gamepad : South button (A on Xbox, X on PlayStation)
    ///   - Mobile  : double-tap of any movement input within <see cref="doubleTapWindow"/>.
    ///               The double-tap detector watches the same Vector2 stream used
    ///               for movement (left stick / WASD / touch-virtual-stick) and
    ///               triggers when the stick is flicked from near-zero to a
    ///               strong direction twice in quick succession.
    /// </summary>
    [RequireComponent(typeof(DashAbility))]
    [DisallowMultipleComponent]
    public class DashInputHandler : MonoBehaviour
    {
        [Header("Buttons")]
        [Tooltip("Additional control paths to bind to the Dash action.")]
        [SerializeField] private string[] extraButtonBindings;

        [Header("Double-tap stick")]
        [Tooltip("Maximum seconds between the two taps for a double-tap to register.")]
        [SerializeField] private float doubleTapWindow = 0.25f;

        [Tooltip("Stick magnitude considered 'released' (back to neutral) between taps.")]
        [Range(0f, 1f)][SerializeField] private float releaseThreshold = 0.25f;

        [Tooltip("Stick magnitude considered a 'tap' (strong push).")]
        [Range(0f, 1f)][SerializeField] private float pressThreshold = 0.7f;

        [Tooltip("Dot product threshold for the second tap to count as 'same direction' as the first.")]
        [Range(-1f, 1f)][SerializeField] private float directionAgreement = 0.6f;

        private DashAbility _ability;
        private InputAction _dashAction;
        private InputAction _moveWatchAction;

        // double-tap state
        private enum TapState { Idle, FirstTapHeld, FirstTapReleased }
        private TapState _tapState = TapState.Idle;
        private float _firstTapTime;
        private Vector2 _firstTapDir;

        private void Awake()
        {
            _ability = GetComponent<DashAbility>();
        }

        private void OnEnable()
        {
            BuildDashAction();
            BuildMoveWatchAction();

            _dashAction.performed += OnDashPerformed;
            _dashAction.Enable();
            _moveWatchAction.Enable();
        }

        private void OnDisable()
        {
            if (_dashAction != null)
            {
                _dashAction.performed -= OnDashPerformed;
                _dashAction.Disable();
                _dashAction.Dispose();
                _dashAction = null;
            }
            if (_moveWatchAction != null)
            {
                _moveWatchAction.Disable();
                _moveWatchAction.Dispose();
                _moveWatchAction = null;
            }
        }

        private void Update()
        {
            if (_moveWatchAction == null) return;
            var v = _moveWatchAction.ReadValue<Vector2>();
            UpdateDoubleTap(v);
        }

        // ---------------- Action wiring ----------------

        private void BuildDashAction()
        {
            _dashAction = new InputAction(name: "Dash", type: InputActionType.Button);
            _dashAction.AddBinding("<Keyboard>/space");
            _dashAction.AddBinding("<Gamepad>/buttonSouth");

            if (extraButtonBindings != null)
            {
                foreach (var path in extraButtonBindings)
                {
                    if (!string.IsNullOrEmpty(path)) _dashAction.AddBinding(path);
                }
            }
        }

        private void BuildMoveWatchAction()
        {
            // A read-only mirror of the project Move action: we don't want to
            // intercept or break the existing PlayerInput wiring, just observe
            // the same input streams.
            _moveWatchAction = new InputAction(name: "DashMoveWatch", type: InputActionType.Value, expectedControlType: "Vector2");
            _moveWatchAction.AddBinding("<Gamepad>/leftStick");

            var composite = _moveWatchAction.AddCompositeBinding("2DVector");
            composite.With("Up", "<Keyboard>/w");
            composite.With("Down", "<Keyboard>/s");
            composite.With("Left", "<Keyboard>/a");
            composite.With("Right", "<Keyboard>/d");
        }

        // ---------------- Callbacks ----------------

        private void OnDashPerformed(InputAction.CallbackContext ctx)
        {
            _ability.TryDash();
        }

        // ---------------- Double-tap state machine ----------------

        private void UpdateDoubleTap(Vector2 v)
        {
            float mag = v.magnitude;
            float now = Time.unscaledTime;

            switch (_tapState)
            {
                case TapState.Idle:
                    if (mag >= pressThreshold)
                    {
                        _tapState = TapState.FirstTapHeld;
                        _firstTapTime = now;
                        _firstTapDir = v.normalized;
                    }
                    break;

                case TapState.FirstTapHeld:
                    if (mag <= releaseThreshold)
                    {
                        _tapState = TapState.FirstTapReleased;
                    }
                    else if (now - _firstTapTime > doubleTapWindow)
                    {
                        // held too long without release - it's just regular movement, abort.
                        _tapState = TapState.Idle;
                    }
                    break;

                case TapState.FirstTapReleased:
                    if (now - _firstTapTime > doubleTapWindow)
                    {
                        _tapState = TapState.Idle;
                        break;
                    }
                    if (mag >= pressThreshold)
                    {
                        Vector2 secondDir = v.normalized;
                        if (Vector2.Dot(secondDir, _firstTapDir) >= directionAgreement)
                        {
                            _ability.TryDash();
                        }
                        _tapState = TapState.Idle;
                    }
                    break;
            }
        }
    }
}
