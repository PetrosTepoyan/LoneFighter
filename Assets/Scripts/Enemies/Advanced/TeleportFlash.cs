using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Advanced
{
    /// One-shot pooled VFX for the teleporter vanish and reappear moments.
    ///
    /// The prefab is expected to carry either a ParticleSystem child (preferred — handled by
    /// FxService-style auto-release) or a SpriteRenderer flash that this script will fade out.
    /// Both paths are supported so artists can swap visuals without touching code.
    public class TeleportFlash : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField] private float duration = 0.25f;

        [Header("Refs (optional — auto-resolved)")]
        [SerializeField] private SpriteRenderer flashRenderer;
        [SerializeField] private Transform flashRoot;
        [SerializeField] private ParticleSystem particles;

        [Header("Animation")]
        [Tooltip("Scale at t=0.")]
        [SerializeField] private float startScale = 0.2f;
        [Tooltip("Scale at t=1.")]
        [SerializeField] private float endScale = 1.4f;
        [SerializeField] private Color startTint = new Color(0.7f, 0.4f, 1f, 0.95f);
        [SerializeField] private Color endTint = new Color(0.7f, 0.4f, 1f, 0f);

        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            if (flashRenderer == null) flashRenderer = GetComponentInChildren<SpriteRenderer>();
            if (flashRoot == null) flashRoot = flashRenderer != null ? flashRenderer.transform : transform;
            if (particles == null) particles = GetComponentInChildren<ParticleSystem>();
        }

        private void OnEnable()
        {
            Play();
        }

        public void Play()
        {
            _elapsed = 0f;
            _playing = true;
            if (flashRenderer != null) flashRenderer.color = startTint;
            if (flashRoot != null) flashRoot.localScale = new Vector3(startScale, startScale, 1f);
            if (particles != null)
            {
                particles.Clear(true);
                particles.Play(true);
            }
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

            if (flashRenderer != null) flashRenderer.color = Color.Lerp(startTint, endTint, t);
            if (flashRoot != null)
            {
                float s = Mathf.Lerp(startScale, endScale, t);
                flashRoot.localScale = new Vector3(s, s, 1f);
            }

            if (t >= 1f)
            {
                _playing = false;
                if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
                else gameObject.SetActive(false);
            }
        }
    }
}
