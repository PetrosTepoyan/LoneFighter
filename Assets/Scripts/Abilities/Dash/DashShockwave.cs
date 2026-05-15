using UnityEngine;

namespace LoneFighter.Abilities
{
    /// <summary>
    /// Small radial pushback emitted at the end of a dash. Pure feel - no damage.
    /// Affects anything with a Rigidbody2D inside <see cref="radius"/>, applying a
    /// gentle outward impulse that falls off linearly with distance.
    ///
    /// Upgrade hooks for a future "damages-enemies-on-pass-through" mod live on
    /// <see cref="DashAbility"/> itself - this script intentionally has no
    /// damage knob to keep the contract simple.
    /// </summary>
    [DisallowMultipleComponent]
    public class DashShockwave : MonoBehaviour
    {
        [Header("Push")]
        [Tooltip("Radius of the shockwave at the dash end position.")]
        [SerializeField] private float radius = 1.6f;

        [Tooltip("Outward impulse magnitude (Rigidbody2D.AddForce, Impulse).")]
        [SerializeField] private float impulse = 4.5f;

        [Tooltip("Layers affected by the shockwave. Leave at Default (everything) " +
                 "and it'll just push every dynamic Rigidbody2D in range.")]
        [SerializeField] private LayerMask affectMask = ~0;

        [Tooltip("If true, only Rigidbody2Ds (not the player itself) are pushed.")]
        [SerializeField] private bool excludeSelf = true;

        // Reused buffer to avoid per-dash allocation.
        private readonly Collider2D[] _buffer = new Collider2D[32];

        /// <summary>Emit the shockwave at the given world position.</summary>
        public void Emit(Vector2 origin)
        {
            int count = Physics2D.OverlapCircleNonAlloc(origin, radius, _buffer, affectMask);
            for (int i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                if (excludeSelf && col.transform == transform) continue;

                var rb = col.attachedRigidbody;
                if (rb == null) continue;
                if (rb.bodyType != RigidbodyType2D.Dynamic) continue;
                if (rb.gameObject == gameObject) continue;

                Vector2 away = (Vector2)rb.worldCenterOfMass - origin;
                float dist = away.magnitude;
                Vector2 dir = dist > 0.0001f ? away / dist : Random.insideUnitCircle.normalized;

                // linear falloff from center -> edge
                float falloff = Mathf.Clamp01(1f - dist / Mathf.Max(0.0001f, radius));
                rb.AddForce(dir * impulse * falloff, ForceMode2D.Impulse);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
