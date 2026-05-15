using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Abilities
{
    /// <summary>
    /// Spawns afterimage sprite ghosts during a dash and fades them out over time.
    /// Each ghost is a lightweight one-shot child GameObject with a SpriteRenderer
    /// copied from the player. We re-use a small pool so a chain of dashes doesn't
    /// allocate GC.
    /// </summary>
    [RequireComponent(typeof(DashAbility))]
    [DisallowMultipleComponent]
    public class DashTrail : MonoBehaviour
    {
        [Header("Spawning")]
        [Tooltip("Number of afterimage ghosts emitted across the dash window.")]
        [SerializeField] private int ghostCount = 5;

        [Tooltip("Total time each ghost takes to fade from peak alpha to zero.")]
        [SerializeField] private float ghostLifetime = 0.4f;

        [Tooltip("Starting alpha of each ghost.")]
        [Range(0f, 1f)][SerializeField] private float startAlpha = 0.65f;

        [Header("Tint")]
        [SerializeField] private Color tint = new Color(0.35f, 0.85f, 1f, 1f);

        [Tooltip("Sorting order delta applied to the ghost so it renders behind the player.")]
        [SerializeField] private int sortingOrderOffset = -1;

        private DashAbility _ability;
        private SpriteRenderer _playerRenderer;
        private readonly Queue<Ghost> _pool = new Queue<Ghost>();
        private readonly List<Ghost> _live = new List<Ghost>(16);

        private float _emitInterval;
        private float _nextEmitTime;
        private int _emittedThisDash;

        private void Awake()
        {
            _ability = GetComponent<DashAbility>();
            _playerRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _ability.OnDashStarted += HandleDashStarted;
            _ability.OnDashEnded += HandleDashEnded;
        }

        private void OnDisable()
        {
            _ability.OnDashStarted -= HandleDashStarted;
            _ability.OnDashEnded -= HandleDashEnded;
        }

        private void Update()
        {
            // emit during a dash
            if (_ability.IsDashing && _emittedThisDash < ghostCount && Time.time >= _nextEmitTime)
            {
                SpawnGhost();
                _emittedThisDash++;
                _nextEmitTime = Time.time + _emitInterval;
            }

            // fade live ghosts
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var g = _live[i];
                g.timeLeft -= dt;
                if (g.timeLeft <= 0f)
                {
                    g.go.SetActive(false);
                    _pool.Enqueue(g);
                    _live.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Clamp01(g.timeLeft / ghostLifetime);
                var c = g.baseColor;
                c.a = c.a * t;
                g.renderer.color = c;
            }
        }

        // ---------------- Internals ----------------

        private void HandleDashStarted(Vector2 _)
        {
            _emittedThisDash = 0;
            float duration = Mathf.Max(0.01f, _ability.DashDuration);
            _emitInterval = ghostCount > 0 ? duration / ghostCount : duration;
            _nextEmitTime = Time.time; // emit the first ghost immediately
        }

        private void HandleDashEnded()
        {
            // any leftover ghosts continue fading naturally
        }

        private void SpawnGhost()
        {
            if (_playerRenderer == null || _playerRenderer.sprite == null) return;

            Ghost ghost = _pool.Count > 0 ? _pool.Dequeue() : CreateGhost();
            ghost.go.SetActive(true);
            ghost.go.transform.position = _playerRenderer.transform.position;
            ghost.go.transform.rotation = _playerRenderer.transform.rotation;
            ghost.go.transform.localScale = _playerRenderer.transform.lossyScale;

            ghost.renderer.sprite = _playerRenderer.sprite;
            ghost.renderer.flipX = _playerRenderer.flipX;
            ghost.renderer.flipY = _playerRenderer.flipY;
            ghost.renderer.sortingLayerID = _playerRenderer.sortingLayerID;
            ghost.renderer.sortingOrder = _playerRenderer.sortingOrder + sortingOrderOffset;

            Color c = tint;
            c.a *= startAlpha;
            ghost.baseColor = c;
            ghost.renderer.color = c;

            ghost.timeLeft = ghostLifetime;
            _live.Add(ghost);
        }

        private Ghost CreateGhost()
        {
            var go = new GameObject("DashGhost");
            go.transform.SetParent(null, worldPositionStays: true);
            var sr = go.AddComponent<SpriteRenderer>();
            return new Ghost { go = go, renderer = sr };
        }

        private void OnDestroy()
        {
            foreach (var g in _live) if (g.go != null) Destroy(g.go);
            while (_pool.Count > 0) { var g = _pool.Dequeue(); if (g.go != null) Destroy(g.go); }
        }

        private class Ghost
        {
            public GameObject go;
            public SpriteRenderer renderer;
            public Color baseColor;
            public float timeLeft;
        }
    }
}
