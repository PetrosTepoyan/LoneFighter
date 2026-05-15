// LoneFighter - Weapon Evolution System
// New file. Do not modify existing files.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Weapons.Evolutions
{
    /// <summary>
    /// Secondary indicator that pulses a "READY TO EVOLVE" badge near the weapon HUD slot
    /// the FIRST time a recipe transitions to "ready" (base weapon maxed + passive owned).
    /// Distinct from <see cref="EvolutionModal"/> - the toast does not stop time, does not
    /// require input, and fires from <see cref="EvolutionService.OnEvolutionReady"/>.
    ///
    /// Wiring:
    ///   - Place this component on a small badge UI object (Image + Text) parented under
    ///     the weapon HUD slot you want it to hover over.
    ///   - Start the scene with the badge GameObject DISABLED; the script enables it.
    ///   - Optionally drag the matching WeaponData into <see cref="filterWeapon"/> to make
    ///     this toast only react to recipes whose base weapon matches. If left null, the
    ///     toast reacts to every recipe (use one toast per HUD slot).
    /// </summary>
    public class EvolutionToast : MonoBehaviour
    {
        [Header("Filter")]
        [Tooltip("Optional. If set, this toast only fires for recipes whose BaseWeapon matches.")]
        [SerializeField] private ScriptableObject filterWeapon;

        [Header("Display")]
        [SerializeField] private GameObject badgeRoot;
        [Tooltip("Optional - UnityEngine.UI.Text or TMP_Text.")]
        [SerializeField] private Component label;
        [SerializeField] private string text = "READY TO EVOLVE";

        [Header("Pulse")]
        [SerializeField] private float pulsePeriodSeconds = 0.85f;
        [SerializeField] private float pulseMinAlpha = 0.55f;
        [SerializeField] private float pulseMaxAlpha = 1.0f;

        private CanvasGroup _canvasGroup;
        private Coroutine _pulseCo;
        private bool _shown;

        private void Awake()
        {
            if (badgeRoot == null) badgeRoot = gameObject;
            _canvasGroup = badgeRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = badgeRoot.AddComponent<CanvasGroup>();
            badgeRoot.SetActive(false);
            SetLabelText(text);
        }

        private void OnEnable()
        {
            EvolutionService.Instance.OnEvolutionReady += HandleEvolutionReady;
            // Surface state immediately if a recipe is already satisfied by the time the
            // HUD spawns (e.g. scene reload).
            EvolutionService.Instance.RefreshReadyState();
        }

        private void OnDisable()
        {
            EvolutionService.Instance.OnEvolutionReady -= HandleEvolutionReady;
            StopPulse();
        }

        private void HandleEvolutionReady(WeaponEvolutionRecipe recipe)
        {
            if (recipe == null) return;
            if (filterWeapon != null && recipe.BaseWeapon != filterWeapon) return;
            if (_shown) return;
            _shown = true;
            badgeRoot.SetActive(true);
            if (_pulseCo == null) _pulseCo = StartCoroutine(PulseLoop());
        }

        /// <summary>Hides the badge - call this when the matching weapon evolves or dies.</summary>
        public void Dismiss()
        {
            _shown = false;
            StopPulse();
            badgeRoot.SetActive(false);
        }

        private void StopPulse()
        {
            if (_pulseCo != null) { StopCoroutine(_pulseCo); _pulseCo = null; }
        }

        private IEnumerator PulseLoop()
        {
            while (badgeRoot.activeInHierarchy)
            {
                float t = Mathf.PingPong(Time.unscaledTime / Mathf.Max(0.01f, pulsePeriodSeconds), 1f);
                _canvasGroup.alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);
                yield return null;
            }
            _pulseCo = null;
        }

        private void SetLabelText(string value)
        {
            if (label == null) return;
            var uiText = label as Text;
            if (uiText != null) { uiText.text = value; return; }
            var prop = label.GetType().GetProperty("text");
            if (prop != null && prop.CanWrite)
            {
                try { prop.SetValue(label, value); } catch { /* ignore */ }
            }
        }
    }
}
