using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.World
{
    /// <summary>
    /// Soft, elastic arena bounds. When the player gets within <see cref="softMargin"/> of an edge
    /// of the rectangle defined by <see cref="center"/>/<see cref="size"/>, this component nudges
    /// their Rigidbody2D back toward the interior. The push scales with how far past the soft edge
    /// the player is, giving a springy "you can't quite leave the arena" feel without hard-stopping
    /// movement (which would feel punishing on a virtual stick).
    ///
    /// Wire this on an empty child of the Arena root. See <c>Assets/Editor/World/ArenaPresetWindow.cs</c>
    /// for one-click scene setup, and <c>Assets/Scripts/World/README.md</c> for tuning notes.
    /// </summary>
    public class ArenaBounds : MonoBehaviour
    {
        [Header("Geometry (world units)")]
        [Tooltip("World-space center of the playable arena.")]
        [SerializeField] private Vector2 center = Vector2.zero;

        [Tooltip("Total width/height of the playable arena. 30x30 matches the camera ortho size 6 reasonably; see README.")]
        [SerializeField] private Vector2 size = new Vector2(30f, 30f);

        [Header("Pushback feel")]
        [Tooltip("Within this distance from each edge, pushback begins. Larger = squishier wall.")]
        [SerializeField] private float softMargin = 1.5f;

        [Tooltip("Maximum inward force applied when fully past the soft edge. Tuned against player moveSpeed (~5).")]
        [SerializeField] private float pushbackStrength = 8f;

        [Tooltip("Hard outer skin past which the player is teleported back inside. Catches edge cases (knockback, scene load) without breaking the elastic feel.")]
        [SerializeField] private float hardClampMargin = 4f;

        [Header("Gizmos")]
        [SerializeField] private Color boundsColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color softMarginColor = new Color(1f, 0.7f, 0.1f, 0.6f);

        public Vector2 Center => center;
        public Vector2 Size => size;
        public float SoftMargin => softMargin;

        private Rigidbody2D _playerRb;
        private Transform _playerTf;

        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (!TryResolvePlayer()) return;

            Vector2 pos = _playerTf.position;
            Vector2 half = size * 0.5f;
            float minX = center.x - half.x;
            float maxX = center.x + half.x;
            float minY = center.y - half.y;
            float maxY = center.y + half.y;

            // How far past the soft edge are we on each axis? Positive = pushing needed.
            // Negative = still safely inside.
            float pushLeft  = (minX + softMargin) - pos.x;   // >0 when player is too far left
            float pushRight = pos.x - (maxX - softMargin);   // >0 when player is too far right
            float pushDown  = (minY + softMargin) - pos.y;   // >0 when too far down
            float pushUp    = pos.y - (maxY - softMargin);   // >0 when too far up

            Vector2 push = Vector2.zero;
            if (pushLeft  > 0f) push.x += pushLeft;
            if (pushRight > 0f) push.x -= pushRight;
            if (pushDown  > 0f) push.y += pushDown;
            if (pushUp    > 0f) push.y -= pushUp;

            if (push.sqrMagnitude > 0f)
            {
                // Normalize the per-axis penetration by softMargin so push∈[0,1] inside the band,
                // then scale by strength. Outside the band (penetration > softMargin) we clamp at 1
                // per-axis to keep the wall springy rather than rocketing the player.
                Vector2 normalized = new Vector2(
                    Mathf.Clamp(push.x / Mathf.Max(0.0001f, softMargin), -1f, 1f),
                    Mathf.Clamp(push.y / Mathf.Max(0.0001f, softMargin), -1f, 1f));

                // AddForce-style: nudge velocity each fixed step. This co-exists with PlayerController
                // setting linearVelocity = input*speed by adding on top of it for the current step.
                _playerRb.linearVelocity += normalized * pushbackStrength * Time.fixedDeltaTime;
            }

            // Hard outer skin: catches teleport/knockback so we never lose the player off-arena.
            float hardMinX = minX - hardClampMargin;
            float hardMaxX = maxX + hardClampMargin;
            float hardMinY = minY - hardClampMargin;
            float hardMaxY = maxY + hardClampMargin;
            if (pos.x < hardMinX || pos.x > hardMaxX || pos.y < hardMinY || pos.y > hardMaxY)
            {
                Vector2 clamped = new Vector2(
                    Mathf.Clamp(pos.x, minX, maxX),
                    Mathf.Clamp(pos.y, minY, maxY));
                _playerRb.position = clamped;
                _playerRb.linearVelocity = Vector2.zero;
            }
        }

        private bool TryResolvePlayer()
        {
            if (_playerRb != null && _playerTf != null) return true;
            var pc = PlayerController.Instance;
            if (pc == null) return false;
            _playerTf = pc.transform;
            _playerRb = pc.GetComponent<Rigidbody2D>();
            return _playerRb != null;
        }

        /// <summary>Public helper so other systems (spawners, hazard placers) can sample a random point inside.</summary>
        public Vector2 RandomPointInside(float padding = 0f)
        {
            Vector2 half = size * 0.5f - Vector2.one * padding;
            return new Vector2(
                center.x + Random.Range(-half.x, half.x),
                center.y + Random.Range(-half.y, half.y));
        }

        /// <summary>Returns true if the world-space position is inside the playable rect (no margin).</summary>
        public bool Contains(Vector2 worldPos)
        {
            Vector2 half = size * 0.5f;
            return worldPos.x >= center.x - half.x && worldPos.x <= center.x + half.x
                && worldPos.y >= center.y - half.y && worldPos.y <= center.y + half.y;
        }

        private void OnDrawGizmosSelected()
        {
            // Playable rect.
            Gizmos.color = boundsColor;
            Gizmos.DrawWireCube(center, size);

            // Soft-margin inner rect (the band where pushback begins).
            Gizmos.color = softMarginColor;
            Vector2 inner = size - Vector2.one * (softMargin * 2f);
            inner.x = Mathf.Max(0.01f, inner.x);
            inner.y = Mathf.Max(0.01f, inner.y);
            Gizmos.DrawWireCube(center, inner);
        }
    }
}
