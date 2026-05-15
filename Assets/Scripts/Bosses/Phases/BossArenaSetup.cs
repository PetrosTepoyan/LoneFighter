using System.Reflection;
using UnityEngine;
using LoneFighter.World;

namespace LoneFighter.Bosses
{
    /// <summary>
    /// Optional cooperator with <see cref="ArenaBounds"/>. While a boss is alive, this component
    /// shrinks the arena's playable rect to <see cref="bossFightSize"/>, then restores the original
    /// size when the boss dies (the encounter is destroyed). Tighter bounds during boss fights
    /// keeps the player from kiting forever and improves shockwave hit-rate.
    ///
    /// Integration:
    ///   * Attach to the boss prefab alongside <see cref="BossEncounter"/>. Resolves the scene's
    ///     <see cref="ArenaBounds"/> via <see cref="Component.TryGetComponent{T}"/> on a parent
    ///     first; falls back to <see cref="Object.FindAnyObjectByType{T}"/>.
    ///   * <see cref="ArenaBounds"/>'s <c>size</c> field is private with no setter. We use
    ///     reflection so we don't have to modify the existing file. If reflection fails (field
    ///     stripped or renamed), we log a setup note and the boss fight uses the default size.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossArenaSetup : MonoBehaviour
    {
        [Header("Arena Override")]
        [Tooltip("Playable rect size to apply while the boss is alive. Default matches a tight square (20x20).")]
        [SerializeField] private Vector2 bossFightSize = new Vector2(20f, 20f);

        [Tooltip("If true, also log the original/restored sizes — useful while tuning encounters.")]
        [SerializeField] private bool verbose = true;

        private ArenaBounds _bounds;
        private Vector2 _originalSize;
        private bool _applied;

        private static FieldInfo _sizeField;
        private static bool _sizeResolved;
        private static bool _sizeMissingLogged;

        private void OnEnable()
        {
            // Resolve arena bounds. Prefer a parent (in case the boss is spawned under the arena
            // root), then fall back to a scene-wide find. We do this in OnEnable rather than Start
            // so pooled bosses re-acquire the bounds on every spawn.
            if (_bounds == null)
            {
                if (!TryGetComponent(out _bounds))
                {
                    _bounds = GetComponentInParent<ArenaBounds>();
                    if (_bounds == null) _bounds = Object.FindAnyObjectByType<ArenaBounds>();
                }
            }

            if (_bounds == null)
            {
                if (verbose) Debug.Log("[BossArenaSetup] No ArenaBounds in scene — skipping arena override.");
                return;
            }

            ResolveSizeField();
            if (_sizeField == null)
            {
                if (verbose) Debug.Log("[BossArenaSetup] ArenaBounds.size field not reflectable; boss fight uses default arena size.");
                return;
            }

            _originalSize = (Vector2)_sizeField.GetValue(_bounds);
            _sizeField.SetValue(_bounds, bossFightSize);
            _applied = true;
            if (verbose) Debug.Log($"[BossArenaSetup] Tightened arena from {_originalSize} → {bossFightSize} for boss fight.");
        }

        private void OnDisable()
        {
            // Boss died / encounter ended → restore.
            if (!_applied || _bounds == null || _sizeField == null) return;
            _sizeField.SetValue(_bounds, _originalSize);
            _applied = false;
            if (verbose) Debug.Log($"[BossArenaSetup] Restored arena size to {_originalSize}.");
        }

        private static void ResolveSizeField()
        {
            if (_sizeResolved) return;
            _sizeField = typeof(ArenaBounds).GetField(
                "size",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _sizeResolved = true;
            if (_sizeField == null && !_sizeMissingLogged)
            {
                Debug.LogWarning(
                    "[BossArenaSetup] Could not reflect ArenaBounds.size — boss arena tightening disabled. " +
                    "Expose a public Size setter on ArenaBounds to enable this.");
                _sizeMissingLogged = true;
            }
        }
    }
}
