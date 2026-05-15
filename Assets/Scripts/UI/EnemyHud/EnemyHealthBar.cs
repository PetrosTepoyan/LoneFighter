using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Enemies;

namespace LoneFighter.UI.EnemyHud
{
    /// <summary>
    /// A small world-following health bar bound to a single <see cref="EnemyHealth"/>.
    ///
    /// Design choices (per spec):
    ///  - The visual is hosted on a <see cref="Canvas"/> set to <c>WorldSpace</c> render mode
    ///    so we can either parent it under the enemy <em>or</em> drive the position manually
    ///    in <see cref="LateUpdate"/> via <see cref="Camera.WorldToScreenPoint"/> when this
    ///    component is not parented to the enemy (see <see cref="useScreenSpaceTracking"/>).
    ///  - The bar fades IN over <see cref="fadeInDuration"/> the moment damage is detected
    ///    (default 0.2s) and fades OUT <see cref="hideDelay"/> seconds (default 2.0s) after
    ///    the last hit. While the alpha is 0 the canvas is disabled to skip its rebuild.
    ///  - <see cref="EnemyHealth"/> exposes <c>OnDied</c> but no <c>OnDamaged</c> event, so
    ///    we POLL <see cref="EnemyHealth.Current"/> every frame and compare against the last
    ///    seen value. This is cheap (one float compare per active enemy) and avoids the need
    ///    to modify <see cref="EnemyHealth"/>. See <see cref="Tick"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Foreground fill image; its fillAmount is driven by Current/Max.")]
        [SerializeField] private Image fillImage;
        [Tooltip("Background image (the darker outline/track behind the fill).")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("CanvasGroup that owns the fade. Auto-grabbed from this GameObject if null.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("The Canvas hosting the bar. Auto-grabbed from this GameObject or parents if null.")]
        [SerializeField] private Canvas canvas;

        [Header("Fade Timing")]
        [Tooltip("Seconds to fade IN to full alpha when damage is taken.")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [Tooltip("Seconds the bar stays visible after the last hit before fading OUT.")]
        [SerializeField] private float hideDelay = 2.0f;
        [Tooltip("Seconds to fade OUT once the hide delay has elapsed.")]
        [SerializeField] private float fadeOutDuration = 0.25f;

        [Header("World Tracking")]
        [Tooltip("If true, this bar is NOT parented to the enemy and instead chases the enemy each LateUpdate by projecting through the main camera. " +
                 "If false, the bar is expected to be parented under the enemy transform and we simply apply the localOffset.")]
        [SerializeField] private bool useScreenSpaceTracking = false;
        [Tooltip("Local offset above the enemy origin (world units).")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.6f, 0f);

        [Header("Behaviour")]
        [Tooltip("Hide the bar entirely once health reaches full again (e.g. after a heal). Otherwise the bar just fades out on the normal timer.")]
        [SerializeField] private bool hideWhenAtFullHealth = true;

        // --- Runtime state -------------------------------------------------------
        private EnemyHealth _target;
        private Transform _targetTransform;
        private float _lastSeenCurrent;
        private float _lastDamageTime = -999f;
        private bool _everDamaged;
        private bool _diedHandlerHooked;
        private Camera _camera;

        // -------------------------------------------------------------------------

        /// <summary>The enemy this bar is currently tracking, or null when idle/pooled.</summary>
        public EnemyHealth Target => _target;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (canvas == null) canvas = GetComponentInChildren<Canvas>();
            // Start hidden — we only appear once damage is taken.
            canvasGroup.alpha = 0f;
            if (canvas != null) canvas.enabled = false;
        }

        private void OnDisable()
        {
            UnhookTarget();
        }

        /// <summary>
        /// Bind this bar to <paramref name="target"/>. The bar resets its state and starts
        /// invisible; it will appear on first damage. Pass null to detach.
        /// </summary>
        public void Bind(EnemyHealth target)
        {
            UnhookTarget();

            _target = target;
            _targetTransform = target != null ? target.transform : null;

            if (_target == null)
            {
                canvasGroup.alpha = 0f;
                if (canvas != null) canvas.enabled = false;
                return;
            }

            _lastSeenCurrent = _target.Current;
            _lastDamageTime = -999f;
            _everDamaged = false;

            _target.OnDied += HandleTargetDied;
            _diedHandlerHooked = true;

            UpdateFillImmediate();
            canvasGroup.alpha = 0f;
            if (canvas != null) canvas.enabled = false;

            if (useScreenSpaceTracking && _camera == null) _camera = Camera.main;

            // Position once on bind so a freshly bound bar isn't visibly snapping next frame.
            SyncPosition();
        }

        private void UnhookTarget()
        {
            if (_diedHandlerHooked && _target != null)
            {
                _target.OnDied -= HandleTargetDied;
            }
            _diedHandlerHooked = false;
            _target = null;
            _targetTransform = null;
        }

        private void HandleTargetDied(EnemyHealth _)
        {
            // The spawner will release us right after this; just hide immediately so we don't
            // flash a stale bar for a frame.
            canvasGroup.alpha = 0f;
            if (canvas != null) canvas.enabled = false;
        }

        /// <summary>
        /// Per-frame logic. Called by the spawner from its own <c>Update</c> so we keep the
        /// MonoBehaviour message overhead off pooled but inactive instances.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_target == null || _targetTransform == null) return;

            // --- Poll for damage. EnemyHealth.Current shrinks monotonically until death; any
            //     observed decrease is treated as a fresh hit and starts the fade-in window.
            float current = _target.Current;
            if (current < _lastSeenCurrent - Mathf.Epsilon)
            {
                _lastDamageTime = Time.time;
                _everDamaged = true;
            }
            _lastSeenCurrent = current;

            UpdateFill();
            UpdateAlpha(deltaTime);
        }

        /// <summary>
        /// Place the bar in the right spot. Called from <see cref="LateUpdate"/> so we read
        /// the enemy's <em>post-physics</em> position, avoiding 1-frame jitter on fast movers.
        /// </summary>
        private void LateUpdate()
        {
            if (_target == null) return;
            SyncPosition();
        }

        private void SyncPosition()
        {
            if (_targetTransform == null) return;

            if (useScreenSpaceTracking)
            {
                // Float in screen-space: project the enemy world position through the camera,
                // then back out to world space at the canvas's Z so the bar lives on a single
                // 2D plane regardless of camera distance.
                if (_camera == null) _camera = Camera.main;
                if (_camera == null) return;
                Vector3 screen = _camera.WorldToScreenPoint(_targetTransform.position + worldOffset);
                // Behind the camera? Hide.
                if (screen.z < 0f)
                {
                    if (canvas != null) canvas.enabled = false;
                    return;
                }
                // Drop back to world coords at the canvas plane.
                screen.z = Mathf.Abs(transform.position.z - _camera.transform.position.z);
                transform.position = _camera.ScreenToWorldPoint(screen);
            }
            else
            {
                // Simple world-space follow: sit just above the enemy origin.
                transform.position = _targetTransform.position + worldOffset;
            }
        }

        private void UpdateFill()
        {
            if (fillImage == null) return;
            float max = Mathf.Max(0.0001f, _target.Max);
            float ratio = Mathf.Clamp01(_target.Current / max);
            fillImage.fillAmount = ratio;
        }

        private void UpdateFillImmediate()
        {
            UpdateFill();
        }

        private void UpdateAlpha(float deltaTime)
        {
            // Three regimes:
            //   1. Never damaged              -> stay invisible (alpha 0).
            //   2. Within hideDelay of a hit  -> fade IN toward 1.
            //   3. Past hideDelay             -> fade OUT toward 0.
            float targetAlpha;
            float speed;

            if (!_everDamaged)
            {
                targetAlpha = 0f;
                speed = 1f / Mathf.Max(0.01f, fadeOutDuration);
            }
            else if (hideWhenAtFullHealth && _target.Current >= _target.Max)
            {
                // Healed back to full — fade out gracefully.
                targetAlpha = 0f;
                speed = 1f / Mathf.Max(0.01f, fadeOutDuration);
            }
            else
            {
                float sinceHit = Time.time - _lastDamageTime;
                if (sinceHit <= hideDelay)
                {
                    targetAlpha = 1f;
                    speed = 1f / Mathf.Max(0.01f, fadeInDuration);
                }
                else
                {
                    targetAlpha = 0f;
                    speed = 1f / Mathf.Max(0.01f, fadeOutDuration);
                }
            }

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed * deltaTime);

            // Disable the canvas entirely when fully faded to skip its rebuild + draw cost.
            if (canvas != null) canvas.enabled = canvasGroup.alpha > 0.001f;
        }

        /// <summary>Editor convenience hook: set up the world offset programmatically.</summary>
        public void SetWorldOffset(Vector3 offset) => worldOffset = offset;
    }
}
