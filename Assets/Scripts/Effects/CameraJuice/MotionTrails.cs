using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace LoneFighter.Effects.CameraJuice
{
    /// <summary>
    /// Camera-positional smear trails for fast-action sequences.
    ///
    /// Records the camera's world position into a small ring buffer each frame
    /// (unscaled), then drives one or more "ghost" follower transforms at fixed
    /// per-ghost delays. While inactive, ghosts are parked at the camera's
    /// current position and disabled, so they cost nothing.
    ///
    /// Two usage modes:
    ///
    /// 1. <b>Auto-spawn:</b> assign <see cref="ghostPrefab"/> (any GameObject —
    ///    typically a SpriteRenderer with a low-alpha tint of the player or a
    ///    full-screen quad). This component will <see cref="Instantiate"/> N
    ///    copies on <see cref="Begin"/>, position them at the recorded history,
    ///    and destroy them on <see cref="End"/>.
    ///
    /// 2. <b>Pre-wired:</b> drop the ghost transforms into <see cref="ghosts"/>
    ///    in the inspector. They will be activated/deactivated by Begin/End,
    ///    and snapped to historical positions every frame while active.
    ///
    /// Note: this only handles the *positions*; the actual smear look comes
    /// from the renderer you put on each ghost (additive sprite, motion blur
    /// shader, etc.). The script is decoupled from the visual.
    /// </summary>
    [DisallowMultipleComponent]
    public class MotionTrails : MonoBehaviour
    {
        public static MotionTrails Instance { get; private set; }

        [Header("Camera")]
        [SerializeField] private CinemachineCamera targetCamera;

        [Header("Ghost Configuration")]
        [Tooltip("Number of ghost copies. Each ghost follows the camera at a different historical delay.")]
        [SerializeField, Range(1, 16)] private int ghostCount = 4;
        [Tooltip("Per-ghost delay step in seconds. Ghost i is delayed by (i+1) * delayStep.")]
        [SerializeField, Range(0.01f, 0.2f)] private float delayStep = 0.03f;

        [Header("Auto-spawn (optional)")]
        [Tooltip("If set, this prefab is instantiated ghostCount times under this transform on Begin().")]
        [SerializeField] private GameObject ghostPrefab;

        [Header("Pre-wired ghosts (optional)")]
        [Tooltip("If non-empty, these transforms are used instead of spawning prefabs.")]
        [SerializeField] private List<Transform> ghosts = new List<Transform>();

        [Header("History")]
        [Tooltip("Ring buffer length. Auto-resized to cover ghostCount * delayStep at 120Hz.")]
        [SerializeField, Range(8, 256)] private int historyCapacity = 64;

        private struct Sample { public Vector3 pos; public float time; }

        private Sample[] _history;
        private int _historyHead; // index of NEXT slot to write
        private int _historyCount;

        private Transform _camTransform;
        private bool _active;
        private readonly List<Transform> _spawned = new List<Transform>();
        private Coroutine _autoEndRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (targetCamera == null) targetCamera = GetComponent<CinemachineCamera>();
            if (targetCamera == null) targetCamera = FindFirstObjectByType<CinemachineCamera>();
            if (targetCamera != null) _camTransform = targetCamera.transform;

            _history = new Sample[Mathf.Max(8, historyCapacity)];
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearSpawned();
        }

        private void LateUpdate()
        {
            if (_camTransform == null) return;
            RecordSample();
            if (_active) DriveGhosts();
        }

        private void RecordSample()
        {
            _history[_historyHead] = new Sample { pos = _camTransform.position, time = Time.unscaledTime };
            _historyHead = (_historyHead + 1) % _history.Length;
            if (_historyCount < _history.Length) _historyCount++;
        }

        private void DriveGhosts()
        {
            int n = ghosts.Count;
            for (int i = 0; i < n; i++)
            {
                var t = ghosts[i];
                if (t == null) continue;
                float delay = (i + 1) * delayStep;
                t.position = SamplePast(delay);
            }
        }

        private Vector3 SamplePast(float delay)
        {
            if (_historyCount == 0 || _camTransform == null) return _camTransform != null ? _camTransform.position : Vector3.zero;

            float targetTime = Time.unscaledTime - delay;

            // Walk backward from most recent until we find a sample older than targetTime,
            // or we exhaust history (in which case return the oldest).
            int idx = (_historyHead - 1 + _history.Length) % _history.Length;
            Sample newer = _history[idx];
            for (int step = 1; step < _historyCount; step++)
            {
                int prev = (idx - 1 + _history.Length) % _history.Length;
                Sample older = _history[prev];
                if (older.time <= targetTime)
                {
                    float span = newer.time - older.time;
                    if (span <= 1e-5f) return older.pos;
                    float k = Mathf.Clamp01((targetTime - older.time) / span);
                    return Vector3.Lerp(older.pos, newer.pos, k);
                }
                idx = prev;
                newer = older;
            }
            return _history[(_historyHead - _historyCount + _history.Length) % _history.Length].pos;
        }

        /// <summary>
        /// Activate the smear trails. Spawns ghosts from <see cref="ghostPrefab"/>
        /// if no pre-wired ghosts are present.
        /// </summary>
        public void Begin()
        {
            if (_active) return;
            _active = true;

            if (ghosts.Count == 0 && ghostPrefab != null)
            {
                for (int i = 0; i < ghostCount; i++)
                {
                    var go = Instantiate(ghostPrefab, transform);
                    go.name = $"{ghostPrefab.name}_ghost{i}";
                    ghosts.Add(go.transform);
                    _spawned.Add(go.transform);
                }
            }

            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts[i] != null) ghosts[i].gameObject.SetActive(true);
            }
        }

        /// <summary>Deactivate the smear trails. Destroys auto-spawned ghosts.</summary>
        public void End()
        {
            if (!_active) return;
            _active = false;

            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts[i] != null) ghosts[i].gameObject.SetActive(false);
            }

            if (_spawned.Count > 0)
            {
                ClearSpawned();
            }
        }

        /// <summary>Activate for a duration of unscaled seconds, then auto-deactivate.</summary>
        public void Burst(float seconds)
        {
            Begin();
            if (_autoEndRoutine != null) StopCoroutine(_autoEndRoutine);
            _autoEndRoutine = StartCoroutine(AutoEnd(Mathf.Max(0.05f, seconds)));
        }

        private IEnumerator AutoEnd(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            End();
            _autoEndRoutine = null;
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    ghosts.Remove(_spawned[i]);
                    Destroy(_spawned[i].gameObject);
                }
            }
            _spawned.Clear();
        }
    }
}
