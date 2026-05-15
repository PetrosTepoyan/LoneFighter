using UnityEngine;
using TMPro;
using LoneFighter.Enemies;

namespace LoneFighter.UI.EnemyHud
{
    /// <summary>
    /// Optional small text label rendered above elite or boss enemies — e.g.
    /// "ELITE GRUNT" or "WARDEN BOSS". Driven by an inspector flag on the prefab
    /// (<see cref="showLabel"/>), so designers can enable per-prefab without code
    /// changes. Auto-fills the label text from <see cref="LoneFighter.Data.EnemyData.displayName"/>
    /// when <see cref="labelText"/> is left blank, prepending an "ELITE " / "BOSS "
    /// prefix based on the <see cref="archetype"/> field.
    ///
    /// This component is meant to be parented under the enemy as a world-space canvas
    /// (or driven by an external follow component); we do not handle world-tracking
    /// here — the label simply renders on the GameObject's transform.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyLabelHud : MonoBehaviour
    {
        public enum Archetype { Normal, Elite, Boss }

        [Header("Inspector Toggle")]
        [Tooltip("Master switch — designers tick this on prefabs that should display a name label.")]
        [SerializeField] private bool showLabel = false;

        [Header("Label")]
        [Tooltip("TextMeshPro element that renders the label.")]
        [SerializeField] private TMP_Text labelText;
        [Tooltip("If non-empty, used verbatim and the archetype prefix is skipped. Otherwise the EnemyData.displayName + archetype prefix is used.")]
        [SerializeField] private string overrideText = "";
        [Tooltip("Determines the prefix prepended to the auto-generated label (e.g. ELITE / BOSS). Normal hides the label entirely.")]
        [SerializeField] private Archetype archetype = Archetype.Elite;

        [Header("Visual Tweaks")]
        [Tooltip("Tint applied to the label text — golden for bosses, white-ish for elites by default.")]
        [SerializeField] private Color eliteColor = new Color(1f, 0.95f, 0.85f, 1f);
        [SerializeField] private Color bossColor  = new Color(1f, 0.84f, 0.27f, 1f);

        [Header("Source")]
        [Tooltip("Source enemy. When null we look for an EnemyBase on this GameObject or any parent.")]
        [SerializeField] private EnemyBase source;

        private bool _initialized;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnValidate()
        {
            // Live-update text in the editor so designers can iterate without entering Play.
            if (isActiveAndEnabled) Refresh();
        }

        /// <summary>
        /// Force a re-evaluation of the label content — call this after changing
        /// <see cref="source"/> or <see cref="archetype"/> at runtime.
        /// </summary>
        public void Refresh()
        {
            if (labelText == null) return;

            if (!showLabel || archetype == Archetype.Normal)
            {
                labelText.gameObject.SetActive(false);
                return;
            }

            if (source == null) source = GetComponentInParent<EnemyBase>();

            string text = !string.IsNullOrEmpty(overrideText)
                ? overrideText
                : BuildAutoText();

            labelText.text = text;
            labelText.color = archetype == Archetype.Boss ? bossColor : eliteColor;
            labelText.gameObject.SetActive(true);
            _initialized = true;
        }

        private string BuildAutoText()
        {
            string baseName = source != null && source.Data != null && !string.IsNullOrEmpty(source.Data.displayName)
                ? source.Data.displayName.ToUpperInvariant()
                : "ENEMY";
            return archetype switch
            {
                Archetype.Boss  => baseName.Contains("BOSS") ? baseName : baseName + " BOSS",
                Archetype.Elite => baseName.StartsWith("ELITE") ? baseName : "ELITE " + baseName,
                _ => baseName,
            };
        }

        /// <summary>Programmatically toggle the label and rebuild.</summary>
        public void SetVisible(bool visible)
        {
            showLabel = visible;
            Refresh();
        }

        /// <summary>Programmatically swap the archetype tier and rebuild.</summary>
        public void SetArchetype(Archetype value)
        {
            archetype = value;
            Refresh();
        }
    }
}
