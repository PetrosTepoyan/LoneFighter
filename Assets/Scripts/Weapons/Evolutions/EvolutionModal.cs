// LoneFighter - Weapon Evolution System
// New file. Do not modify existing files.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Weapons.Evolutions
{
    /// <summary>
    /// Modal panel that pops up on top of gameplay when <see cref="EvolutionService.OnEvolutionAvailable"/>
    /// fires. Mirrors the LevelUpModal interaction model:
    ///   - sets <c>Time.timeScale = 0</c> while open
    ///   - drives animations / input via unscaled time
    ///   - exposes a single "Evolve" button and a "Skip" button
    ///
    /// Wiring:
    ///   1. Attach to a UI panel GameObject that is INACTIVE in the scene.
    ///   2. Drag-link <see cref="evolveButton"/>, <see cref="skipButton"/>,
    ///      <see cref="titleLabel"/>, <see cref="bodyLabel"/>.
    ///   3. The script subscribes to the service in OnEnable; multiple recipes that arrive
    ///      while the modal is open are queued and shown one after the other.
    ///
    /// On "Evolve" the modal calls <see cref="EvolutionService.ApplyEvolution"/> which does
    /// the inventory swap. If the inventory does not support removing the base weapon, the
    /// modal logs the limitation via Debug.LogWarning so it surfaces in the runtime console.
    /// </summary>
    public class EvolutionModal : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button evolveButton;
        [SerializeField] private Button skipButton;

        [Header("Labels (optional - assign whichever UI text component your project uses)")]
        [Tooltip("Drag a UnityEngine.UI.Text or TMP_Text here. The script writes via GetComponent.")]
        [SerializeField] private Component titleLabel;
        [SerializeField] private Component bodyLabel;

        [Header("Root")]
        [Tooltip("The root GameObject to enable/disable. Leave empty to use this component's GameObject.")]
        [SerializeField] private GameObject panelRoot;

        private readonly Queue<WeaponEvolutionRecipe> _queue = new Queue<WeaponEvolutionRecipe>();
        private WeaponEvolutionRecipe _current;
        private bool _isOpen;
        private float _previousTimeScale = 1f;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            if (panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EvolutionService.Instance.OnEvolutionAvailable += HandleEvolutionAvailable;
            if (evolveButton != null) evolveButton.onClick.AddListener(OnEvolveClicked);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
        }

        private void OnDisable()
        {
            EvolutionService.Instance.OnEvolutionAvailable -= HandleEvolutionAvailable;
            if (evolveButton != null) evolveButton.onClick.RemoveListener(OnEvolveClicked);
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkipClicked);
        }

        private void HandleEvolutionAvailable(WeaponEvolutionRecipe recipe)
        {
            if (recipe == null) return;
            _queue.Enqueue(recipe);
            if (!_isOpen) ShowNext();
        }

        private void ShowNext()
        {
            if (_queue.Count == 0)
            {
                Close();
                return;
            }
            _current = _queue.Dequeue();
            Open();
            PopulateLabels(_current);
        }

        private void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            panelRoot.SetActive(true);
        }

        private void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            panelRoot.SetActive(false);
            Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
            _current = null;
        }

        private void OnEvolveClicked()
        {
            var recipe = _current;
            if (recipe == null) { AdvanceQueue(); return; }

            var applied = EvolutionService.Instance.ApplyEvolution(recipe);
            if (applied && !EvolutionService.Instance.DidRemoveBaseWeapon)
            {
                Debug.LogWarning(
                    "[EvolutionModal] Granted evolved weapon but could not remove base weapon. " +
                    "WeaponInventory may not expose a RemoveWeapon API; the base weapon will keep firing.");
            }
            AdvanceQueue();
        }

        private void OnSkipClicked() => AdvanceQueue();

        private void AdvanceQueue()
        {
            if (_queue.Count > 0) ShowNext();
            else Close();
        }

        private void PopulateLabels(WeaponEvolutionRecipe recipe)
        {
            SetLabelText(titleLabel, "Weapon Evolution Available!");
            var body = "Evolve " + SafeName(recipe.BaseWeapon) +
                       " into " + SafeName(recipe.EvolvedWeapon) + "?";
            SetLabelText(bodyLabel, body);
        }

        private static string SafeName(Object o) => o == null ? "(missing)" : o.name;

        private static void SetLabelText(Component label, string text)
        {
            if (label == null) return;
            // UnityEngine.UI.Text
            var uiText = label as Text;
            if (uiText != null) { uiText.text = text; return; }

            // TMP_Text (reflected - avoids hard reference to TextMeshPro)
            var t = label.GetType();
            var prop = t.GetProperty("text");
            if (prop != null && prop.CanWrite)
            {
                try { prop.SetValue(label, text); } catch { /* ignore */ }
            }
        }
    }
}
