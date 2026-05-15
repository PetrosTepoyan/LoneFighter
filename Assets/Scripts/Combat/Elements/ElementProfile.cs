using UnityEngine;

namespace LoneFighter.Combat.Elements
{
    /// <summary>
    /// Authoring data describing the presentation and status-effect mapping of a single <see cref="Element"/>.
    /// Created via the menu "LoneFighter / Combat / Generate Element Profiles" (see editor script).
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Combat/Element Profile", fileName = "ElementProfile", order = 100)]
    public sealed class ElementProfile : ScriptableObject
    {
        [SerializeField] private Element element = Element.Physical;
        [SerializeField] private string displayName = "Physical";

        [ColorUsage(showAlpha: true, hdr: true)]
        [SerializeField] private Color tint = Color.white;

        [Tooltip("Optional VFX prefab spawned on hit (impact / burst particles).")]
        [SerializeField] private GameObject particlePrefab;

        [Tooltip("Type name of the status effect MonoBehaviour to add to the enemy on hit, " +
                 "e.g. \"BurnEffect\". Resolved at runtime via reflection by ElementalDamageDispatcher. " +
                 "Leave empty for elements that should not inflict a status (e.g. Physical).")]
        [SerializeField] private string statusEffectTypeName = string.Empty;

        public Element Element => element;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? element.ToString() : displayName;
        public Color Tint => tint;
        public GameObject ParticlePrefab => particlePrefab;
        public string StatusEffectTypeName => statusEffectTypeName;

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the profile generator.</summary>
        public void EditorPopulate(Element e, string display, Color color, string statusType)
        {
            element = e;
            displayName = display;
            tint = color;
            statusEffectTypeName = statusType ?? string.Empty;
        }
#endif
    }
}
