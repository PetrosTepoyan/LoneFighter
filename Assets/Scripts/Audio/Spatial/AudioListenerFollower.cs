// AudioListenerFollower.cs
// LoneFighter — Spatial Audio module
// Drives the scene's AudioListener to follow the player every LateUpdate so
// that 3D SFX panning/attenuation are computed from the player's position
// instead of the camera (which in this game is offset / sometimes static).
//
// Hard rules: does NOT modify the camera, GameManager, PlayerController, or
// any existing component. This is a drop-in MonoBehaviour — attach it to any
// persistent GameObject in the gameplay scene (or let it be auto-spawned by
// the bootstrap if you wire one up).

using UnityEngine;

namespace LoneFighter.Audio.Spatial
{
    [DisallowMultipleComponent]
    public sealed class AudioListenerFollower : MonoBehaviour
    {
        [Tooltip("Z coordinate forced on the listener each frame. Use -10 to match the URP 2D camera plane, or 0 for pure top-down.")]
        [SerializeField] private float _listenerZ = -10f;

        [Tooltip("How often (seconds) to re-search for the player if it hasn't been found yet. Use 0 to search every LateUpdate.")]
        [SerializeField] private float _playerSearchInterval = 0.25f;

        private AudioListener _listener;
        private Transform _playerTransform;
        private float _nextSearchTime;

        private void Start()
        {
            ResolveListener();
            ResolvePlayer();
        }

        private void LateUpdate()
        {
            if (_listener == null)
            {
                ResolveListener();
                if (_listener == null) return;
            }

            if (_playerTransform == null)
            {
                if (Time.unscaledTime >= _nextSearchTime)
                {
                    ResolvePlayer();
                    _nextSearchTime = Time.unscaledTime + _playerSearchInterval;
                }
                if (_playerTransform == null) return;
            }

            // Position-only follow. Do NOT touch rotation — Unity's default
            // listener orientation is fine for top-down panning.
            Vector3 p = _playerTransform.position;
            p.z = _listenerZ;
            _listener.transform.position = p;
        }

        private void ResolveListener()
        {
            // Prefer the listener tagged on the main camera (Unity's default setup),
            // then fall back to any active AudioListener in the scene.
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var camListener = mainCam.GetComponent<AudioListener>();
                if (camListener != null && camListener.enabled)
                {
                    _listener = camListener;
                    return;
                }
            }

            _listener = FindFirstObjectByType<AudioListener>();
        }

        private void ResolvePlayer()
        {
            // We resolve PlayerController.Instance reflectively so this module
            // compiles even if PlayerController hasn't been added to the project
            // yet (e.g. on a feature branch). Once present, it just works.
            var pc = TryGetPlayerControllerInstance();
            if (pc is Component c)
            {
                _playerTransform = c.transform;
                return;
            }

            // Fallback: search by tag.
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                _playerTransform = tagged.transform;
            }
        }

        private static object TryGetPlayerControllerInstance()
        {
            // Reflective lookup of LoneFighter.PlayerController.Instance (or any
            // namespace) to avoid a hard compile-time dependency on the player
            // module from this audio-only assembly/folder.
            var asms = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                var t = asms[i].GetType("LoneFighter.Player.PlayerController", false)
                     ?? asms[i].GetType("LoneFighter.PlayerController",        false)
                     ?? asms[i].GetType("PlayerController",                    false);
                if (t == null) continue;

                var prop = t.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    return prop.GetValue(null, null);
                }
                var field = t.GetField("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null);
                }
            }
            return null;
        }
    }
}
