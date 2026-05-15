using UnityEngine;

namespace LoneFighter.Enemies.Advanced
{
    /// Visual marker that floats above a HealerEnemy and reads "PRIORITY" so the player learns
    /// to kill them first. Drawn via a child SpriteRenderer (chevron/exclamation icon) plus an
    /// optional TextMesh — whichever the prefab artist chose. We do not hard-depend on TMP to
    /// keep this component drop-in.
    ///
    /// Behavior:
    /// - Bobs vertically and gently pulses scale so it stands out at a glance even in a crowd.
    /// - Tints to red while the healer is at or above its preferred range from the player (i.e.
    ///   the healer is actively threatening). Falls back to yellow if PlayerController is missing.
    [DisallowMultipleComponent]
    public class HealerPriorityIndicator : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Transform to bob/scale (typically a child holding the icon and/or text).")]
        [SerializeField] private Transform indicatorRoot;
        [Tooltip("Optional renderer for the icon. Tinted between idleColor and alertColor.")]
        [SerializeField] private SpriteRenderer iconRenderer;

        [Header("Bob")]
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobFrequency = 2.4f;

        [Header("Pulse")]
        [SerializeField] private float pulseAmplitude = 0.08f;
        [SerializeField] private float pulseFrequency = 3.0f;

        [Header("Colors")]
        [SerializeField] private Color idleColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color alertColor = new Color(1f, 0.25f, 0.25f, 1f);
        [Tooltip("Distance from player at which the indicator turns fully red (alert).")]
        [SerializeField] private float alertDistance = 5f;

        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private bool _captured;
        private float _phase;

        private void Awake()
        {
            if (indicatorRoot == null) indicatorRoot = transform;
            if (iconRenderer == null) iconRenderer = GetComponentInChildren<SpriteRenderer>();
            _basePosition = indicatorRoot.localPosition;
            _baseScale = indicatorRoot.localScale;
            _captured = true;
            // Random phase per instance so a horde of healers doesn't pulse in unison.
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnEnable()
        {
            // Re-snap to original local pose every time the healer comes out of the pool.
            if (_captured && indicatorRoot != null)
            {
                indicatorRoot.localPosition = _basePosition;
                indicatorRoot.localScale = _baseScale;
            }
            if (iconRenderer != null) iconRenderer.color = idleColor;
        }

        private void LateUpdate()
        {
            if (indicatorRoot == null) return;

            float t = Time.time + _phase;

            // Bob.
            Vector3 pos = _basePosition;
            pos.y += Mathf.Sin(t * Mathf.PI * 2f * bobFrequency) * bobAmplitude;
            indicatorRoot.localPosition = pos;

            // Pulse scale.
            float s = 1f + Mathf.Sin(t * Mathf.PI * 2f * pulseFrequency) * pulseAmplitude;
            indicatorRoot.localScale = new Vector3(_baseScale.x * s, _baseScale.y * s, _baseScale.z);

            // Alert tint.
            if (iconRenderer != null)
            {
                float alert = 0f;
                var player = LoneFighter.Player.PlayerController.Instance;
                if (player != null && alertDistance > 0f)
                {
                    float d = ((Vector2)player.transform.position - (Vector2)transform.position).magnitude;
                    alert = Mathf.Clamp01(1f - (d / alertDistance));
                }
                iconRenderer.color = Color.Lerp(idleColor, alertColor, alert);
            }
        }
    }
}
