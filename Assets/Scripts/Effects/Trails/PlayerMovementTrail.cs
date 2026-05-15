using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Effects.Trails
{
    /// <summary>
    /// Emits short-lived afterimage "ghost" sprites behind the player when they
    /// are moving fast enough. Pure visual juice — no gameplay side-effects.
    ///
    /// Ghosts are pooled internally (a small ring buffer of <see cref="SpriteRenderer"/>
    /// children) so we do not allocate on the hot path. Each ghost fades from
    /// <see cref="startAlpha"/> to 0 over <see cref="ghostLifetime"/> seconds and is
    /// then recycled.
    ///
    /// Wire-up:
    /// - Drop this component anywhere in the scene (it does not need to be
    ///   parented to the player).
    /// - On Start it grabs the player's <see cref="SpriteRenderer"/> via
    ///   <see cref="PlayerController.Instance"/>; if the player is not yet
    ///   present it keeps polling until it appears.
    /// - The pool of <see cref="GhostSlot"/>s is sized at <see cref="poolSize"/>;
    ///   if you crank the sample rate or lifetime, raise this so older ghosts
    ///   aren't recycled before they fade.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class PlayerMovementTrail : MonoBehaviour
    {
        [Header("Sampling")]
        [Tooltip("Seconds between ghost samples. Default 0.05 s = 20 Hz.")]
        [Min(0.001f)]
        [SerializeField] private float sampleInterval = 0.05f;

        [Tooltip("Minimum player linear velocity (units / second) required to emit ghosts.")]
        [Min(0f)]
        [SerializeField] private float velocityThreshold = 3.5f;

        [Header("Ghost Appearance")]
        [Tooltip("How long each ghost remains visible before being recycled.")]
        [Min(0.01f)]
        [SerializeField] private float ghostLifetime = 0.4f;

        [Tooltip("Alpha at spawn time. Fades linearly to 0 over ghostLifetime.")]
        [Range(0f, 1f)]
        [SerializeField] private float startAlpha = 0.45f;

        [Tooltip("Tint multiplied with the captured sprite color. White = pure afterimage; " +
                 "use a saturated color for a stylized speed trail.")]
        [SerializeField] private Color tint = new(0.65f, 0.85f, 1.0f, 1f);

        [Tooltip("Sorting order offset relative to the player sprite. Negative = behind player.")]
        [SerializeField] private int sortingOrderOffset = -1;

        [Header("Pool")]
        [Tooltip("Maximum number of simultaneously visible ghosts.")]
        [Min(1)]
        [SerializeField] private int poolSize = 12;

        // Internal pool entry. One SpriteRenderer per slot, reused round-robin.
        private sealed class GhostSlot
        {
            public SpriteRenderer Renderer;
            public float Age;
            public bool Active;
        }

        private readonly List<GhostSlot> _slots = new();
        private int _nextSlot;

        private float _sampleTimer;
        private Transform _playerTf;
        private SpriteRenderer _playerSr;
        private Rigidbody2D _playerRb;

        private void Start()
        {
            TryBindPlayer();
        }

        private void TryBindPlayer()
        {
            var player = PlayerController.Instance;
            if (player == null) return;
            _playerTf = player.transform;
            _playerSr = player.GetComponentInChildren<SpriteRenderer>();
            _playerRb = player.GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            // Always advance and fade existing ghosts, even when the player is gone.
            UpdateGhosts(Time.deltaTime);

            if (_playerTf == null || _playerSr == null)
            {
                TryBindPlayer();
                if (_playerTf == null || _playerSr == null) return;
            }

            // Velocity check: prefer Rigidbody2D.linearVelocity (Unity 6 API), fall back
            // to the player's MoveSpeed-driven Transform delta if no rigidbody is present.
            float speedSq;
            if (_playerRb != null)
            {
                Vector2 v = _playerRb.linearVelocity;
                speedSq = v.sqrMagnitude;
            }
            else
            {
                // No rigidbody available — skip emission rather than guess.
                speedSq = 0f;
            }

            if (speedSq < velocityThreshold * velocityThreshold)
            {
                // Reset the timer so we don't burst-spawn the instant the player accelerates again.
                _sampleTimer = sampleInterval;
                return;
            }

            _sampleTimer -= Time.deltaTime;
            if (_sampleTimer > 0f) return;
            _sampleTimer = sampleInterval;

            EmitGhost();
        }

        private void EmitGhost()
        {
            if (_playerSr == null || _playerSr.sprite == null) return;

            GhostSlot slot = AcquireSlot();
            SpriteRenderer sr = slot.Renderer;

            sr.sprite = _playerSr.sprite;
            sr.flipX = _playerSr.flipX;
            sr.flipY = _playerSr.flipY;
            sr.transform.SetPositionAndRotation(_playerTf.position, _playerTf.rotation);
            sr.transform.localScale = _playerTf.lossyScale;

            // Color: captured sprite color * authored tint, with our alpha.
            Color baseCol = _playerSr.color * tint;
            baseCol.a = startAlpha;
            sr.color = baseCol;

            sr.sortingLayerID = _playerSr.sortingLayerID;
            sr.sortingOrder = _playerSr.sortingOrder + sortingOrderOffset;
            sr.enabled = true;

            slot.Age = 0f;
            slot.Active = true;
        }

        private GhostSlot AcquireSlot()
        {
            // Grow the pool lazily until we hit poolSize, then recycle round-robin.
            if (_slots.Count < poolSize)
            {
                var go = new GameObject($"GhostSlot_{_slots.Count}");
                go.transform.SetParent(transform, worldPositionStays: false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.enabled = false;
                var slot = new GhostSlot { Renderer = sr };
                _slots.Add(slot);
                return slot;
            }

            // Round-robin over the existing pool. Note: this will steal a slot from
            // any still-visible ghost when we're saturated — that's the intended
            // trade-off, and at default settings (12 slots * 0.05 s = 0.6 s of history,
            // 0.4 s lifetime) it never triggers.
            var pick = _slots[_nextSlot];
            _nextSlot = (_nextSlot + 1) % _slots.Count;
            return pick;
        }

        private void UpdateGhosts(float dt)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (!s.Active) continue;

                s.Age += dt;
                float t = Mathf.Clamp01(s.Age / ghostLifetime);
                if (t >= 1f)
                {
                    s.Active = false;
                    s.Renderer.enabled = false;
                    continue;
                }

                Color c = s.Renderer.color;
                c.a = (1f - t) * startAlpha;
                s.Renderer.color = c;
            }
        }
    }
}
