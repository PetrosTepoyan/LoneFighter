using System.Collections;
using UnityEngine;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Hidden helper MonoBehaviour that owns coroutines kicked off by
    /// ScriptableObject patterns (which can't run coroutines themselves).
    /// Auto-spawns on first access, persists across scene loads, and lives
    /// under <c>DontDestroyOnLoad</c>.
    ///
    /// Patterns that need delayed shots (e.g. <c>AimedBurst</c>) call
    /// <see cref="Run"/> with their <c>IEnumerator</c> instead of
    /// <c>StartCoroutine</c>.
    /// </summary>
    public class PatternCoroutineRunner : MonoBehaviour
    {
        private static PatternCoroutineRunner _instance;

        public static PatternCoroutineRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[PatternCoroutineRunner]");
                _instance = go.AddComponent<PatternCoroutineRunner>();
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                return _instance;
            }
        }

        /// <summary>Start a coroutine on the shared runner.</summary>
        public static Coroutine Run(IEnumerator routine)
        {
            if (routine == null) return null;
            return Instance.StartCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
