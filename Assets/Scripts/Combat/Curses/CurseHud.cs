// CurseHud.cs — top-corner strip of icons that surfaces the active curses
// during a run, plus an optional vignette overlay that responds to the
// DenseFog curse.
//
// Wiring (do this in the Editor, see Combat/Curses/README):
//   * Drop this component on a GameObject inside the in-game HUD canvas.
//   * Assign:
//       - iconPrefab: a prefab with an Image. The icon strip is rebuilt
//         from CurseManager.ActiveCurses when the run starts.
//       - iconContainer: a horizontal LayoutGroup transform.
//       - vignetteOverlay (optional): a full-screen Image (RectTransform
//         stretched, alpha 0). We fade it in for DenseFog.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Systems;

namespace LoneFighter.Combat.Curses
{
    /// <summary>
    /// In-run HUD strip that displays the player's active curses.
    /// </summary>
    public class CurseHud : MonoBehaviour
    {
        [Header("Icon strip")]
        [Tooltip("Prefab for a single curse icon. Should contain an Image. " +
                 "Spawned once per active curse.")]
        [SerializeField] private GameObject iconPrefab;

        [Tooltip("Parent transform under which icon instances are spawned. " +
                 "Typically a HorizontalLayoutGroup in the top corner of the HUD.")]
        [SerializeField] private Transform iconContainer;

        [Header("DenseFog vignette")]
        [Tooltip("Optional full-screen Image used as a vignette overlay. " +
                 "Enabled and faded in when the DenseFog curse is active.")]
        [SerializeField] private Image vignetteOverlay;

        [Tooltip("Target alpha for the vignette overlay when DenseFog is active.")]
        [Range(0f, 1f)]
        [SerializeField] private float vignetteAlpha = 0.65f;

        [Tooltip("Seconds for the vignette to fade in.")]
        [SerializeField] private float vignetteFadeDuration = 0.6f;

        // Spawned icon roots so we can tear them down between runs.
        private readonly List<GameObject> _spawnedIcons = new();

        private bool _subscribed;
        private float _vignetteFadeT;
        private bool _vignetteFadingIn;

        private void OnEnable()
        {
            TrySubscribeGameManager();
            CurseManager.OnEffectsChanged += HandleEffectsChanged;

            // Initial state — covers the case where the run is already in
            // progress when this HUD is enabled.
            Refresh();
        }

        private void OnDisable()
        {
            if (_subscribed && GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
                _subscribed = false;
            }
            CurseManager.OnEffectsChanged -= HandleEffectsChanged;
        }

        private void TrySubscribeGameManager()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            _subscribed = true;
        }

        private void Start() => TrySubscribeGameManager();

        private void HandleStateChanged(GameState prev, GameState next)
        {
            // Refresh both on entering Playing (active set has been applied)
            // and on entering GameOver/Victory (so the HUD tears down).
            if (next == GameState.Playing || next == GameState.GameOver || next == GameState.Victory)
            {
                Refresh();
            }
        }

        private void HandleEffectsChanged() => Refresh();

        // ---- Icon strip --------------------------------------------------

        private void Refresh()
        {
            RebuildIcons();
            UpdateVignetteTarget();
        }

        private void RebuildIcons()
        {
            // Tear down previous icons. We rebuild from scratch rather than
            // diff-ing because the strip only updates at run boundaries and
            // it keeps the code straightforward.
            for (int i = _spawnedIcons.Count - 1; i >= 0; i--)
            {
                if (_spawnedIcons[i] != null) Destroy(_spawnedIcons[i]);
            }
            _spawnedIcons.Clear();

            if (iconPrefab == null || iconContainer == null) return;
            if (CurseManager.Instance == null) return;

            var curses = CurseManager.Instance.ActiveCurses;
            for (int i = 0; i < curses.Count; i++)
            {
                var c = curses[i];
                if (c == null) continue;
                var go = Instantiate(iconPrefab, iconContainer);
                go.name = "CurseIcon_" + c.id;
                var image = go.GetComponentInChildren<Image>(includeInactive: true);
                if (image != null && c.icon != null)
                {
                    image.sprite = c.icon;
                }
                _spawnedIcons.Add(go);
            }
        }

        // ---- DenseFog vignette -------------------------------------------

        private void UpdateVignetteTarget()
        {
            if (vignetteOverlay == null) return;
            if (CurseManager.DenseFog)
            {
                vignetteOverlay.gameObject.SetActive(true);
                _vignetteFadingIn = true;
                _vignetteFadeT = 0f;
            }
            else
            {
                _vignetteFadingIn = false;
                var col = vignetteOverlay.color;
                col.a = 0f;
                vignetteOverlay.color = col;
                vignetteOverlay.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_vignetteFadingIn || vignetteOverlay == null) return;
            if (vignetteFadeDuration <= 0f)
            {
                var c = vignetteOverlay.color; c.a = vignetteAlpha; vignetteOverlay.color = c;
                _vignetteFadingIn = false;
                return;
            }
            _vignetteFadeT += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_vignetteFadeT / vignetteFadeDuration);
            var col = vignetteOverlay.color;
            col.a = Mathf.Lerp(0f, vignetteAlpha, t);
            vignetteOverlay.color = col;
            if (t >= 1f) _vignetteFadingIn = false;
        }
    }
}
