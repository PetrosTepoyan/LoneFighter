using TMPro;
using UnityEngine;

namespace LoneFighter.Localization
{
    /// <summary>
    /// Drop this onto any GameObject that has a <see cref="TMP_Text"/> (UGUI or 3D variant).
    /// On <see cref="OnEnable"/> and on every <see cref="LocalizationService.OnLocaleChanged"/>,
    /// the text is replaced with the localized value for <see cref="localizationKey"/>.
    ///
    /// Authoring flow:
    ///  1. Drag a `LocalizedText` onto the TMP_Text GameObject.
    ///  2. Type a key from <see cref="LocalizationKeys"/>, e.g. <c>ui.play</c>.
    ///  3. (Optional) leave the original TMP text as a sensible default — it's used as the
    ///     fallback if the key is missing in every locale.
    ///
    /// The component **does not** modify the existing UI prefab; it can be added alongside
    /// pre-authored TMP text without breaking layouts or styles.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LoneFighter/Localization/Localized Text")]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Canonical key — see LocalizationKeys.cs. Example: \"ui.play\", \"hud.kills\".")]
        [SerializeField] private string localizationKey;

        [Tooltip("Optional. If empty, the TMP_Text's authoring-time text is captured on Awake and used as the fallback when the key is missing.")]
        [SerializeField] private string fallback;

        [Tooltip("If true, refreshes the string in OnEnable. Almost always desired — disable only for one-shot baked text.")]
        [SerializeField] private bool refreshOnEnable = true;

        private TMP_Text _tmp;
        private string _originalText;
        private bool _subscribed;

        /// <summary>Read/write key. Setting this in code immediately re-applies the new string.</summary>
        public string LocalizationKey
        {
            get => localizationKey;
            set
            {
                localizationKey = value;
                if (isActiveAndEnabled) Apply();
            }
        }

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            if (_tmp == null)
            {
                Debug.LogWarning($"[LocalizedText] No TMP_Text component on '{name}'. Disabling.", this);
                enabled = false;
                return;
            }

            // Capture the authoring-time text as a fallback if none was provided in the inspector.
            _originalText = _tmp.text;
            if (string.IsNullOrEmpty(fallback)) fallback = _originalText;
        }

        private void OnEnable()
        {
            Subscribe();
            if (refreshOnEnable) Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            LocalizationService.Instance.OnLocaleChanged += Apply;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            // Avoid touching Instance if domain is tearing down.
            try { LocalizationService.Instance.OnLocaleChanged -= Apply; }
            catch { /* domain reload edge cases */ }
            _subscribed = false;
        }

        /// <summary>Re-pull the localized string and write it to the TMP_Text. Public so editor scripts / tests can invoke it.</summary>
        public void Apply()
        {
            if (_tmp == null) return;
            var fb = !string.IsNullOrEmpty(fallback) ? fallback : _originalText;
            _tmp.text = LocalizationService.Instance.Get(localizationKey, fb);
        }
    }
}
