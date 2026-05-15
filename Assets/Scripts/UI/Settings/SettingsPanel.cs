using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LoneFighter.Systems;

namespace LoneFighter.UI.Settings
{
    /// <summary>
    /// UI controller for the settings panel. Reads from / writes to <see cref="GameSettings"/>
    /// and calls <see cref="GameSettings.Apply"/> after every change so the player gets
    /// instant feedback (audio attenuation, FPS cap, quality level).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Toggles")]
        [SerializeField] private Toggle hapticsToggle;

        [Header("Dropdowns")]
        [SerializeField] private TMP_Dropdown fpsDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Events")]
        [Tooltip("Invoked when the close button is pressed. Parent controller decides what 'close' means.")]
        [SerializeField] private UnityEvent onClose = new UnityEvent();

        [Header("Audio Preview (optional)")]
        [Tooltip("Clip played when the master or sfx slider is released so the player can hear the new volume.")]
        [SerializeField] private AudioClip previewClickClip;

        // Mapping from dropdown index to FPS value. Keep in sync with the populated options.
        private static readonly int[] FpsValues = { 60, 90, 120, -1 };
        private static readonly string[] FpsLabels = { "60 fps", "90 fps", "120 fps", "Uncapped" };
        private static readonly string[] QualityLabels = { "Low", "Medium", "High" };

        // Reentrancy guard: ApplyToUi() programmatically sets every control, which would
        // re-fire onValueChanged and bounce the value through GameSettings again.
        private bool _suppressCallbacks;

        private void OnEnable()
        {
            BindCallbacks();
            ApplyToUi();
        }

        private void OnDisable()
        {
            UnbindCallbacks();
        }

        private void BindCallbacks()
        {
            if (masterSlider != null)
            {
                masterSlider.minValue = 0f;
                masterSlider.maxValue = 1f;
                masterSlider.onValueChanged.AddListener(OnMasterChanged);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SettingsPanel)}] masterSlider is not assigned.", this);
            }

            if (musicSlider != null)
            {
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 1f;
                musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SettingsPanel)}] musicSlider is not assigned.", this);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SettingsPanel)}] sfxSlider is not assigned.", this);
            }

            if (hapticsToggle != null)
            {
                hapticsToggle.onValueChanged.AddListener(OnHapticsChanged);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SettingsPanel)}] hapticsToggle is not assigned.", this);
            }

            if (fpsDropdown != null)
            {
                PopulateDropdown(fpsDropdown, FpsLabels);
                fpsDropdown.onValueChanged.AddListener(OnFpsChanged);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SettingsPanel)}] fpsDropdown is not assigned.", this);
            }

            if (qualityDropdown != null)
            {
                PopulateDropdown(qualityDropdown, QualityLabels);
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SettingsPanel)}] qualityDropdown is not assigned.", this);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SettingsPanel)}] closeButton is not assigned.", this);
            }

            // Slider "release" preview — wire pointer-up via EventTrigger because Unity's
            // built-in Slider only fires onValueChanged continuously, not on release.
            AddSliderReleaseListener(masterSlider, PreviewClick);
            AddSliderReleaseListener(sfxSlider, PreviewClick);
        }

        private void UnbindCallbacks()
        {
            if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            if (musicSlider != null)  musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            if (sfxSlider != null)    sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (hapticsToggle != null) hapticsToggle.onValueChanged.RemoveListener(OnHapticsChanged);
            if (fpsDropdown != null)  fpsDropdown.onValueChanged.RemoveListener(OnFpsChanged);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
            if (closeButton != null)  closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary>Push current <see cref="GameSettings"/> values into the visible controls.</summary>
        public void ApplyToUi()
        {
            _suppressCallbacks = true;
            try
            {
                if (masterSlider != null)   masterSlider.value = GameSettings.MasterVolume;
                if (musicSlider != null)    musicSlider.value  = GameSettings.MusicVolume;
                if (sfxSlider != null)      sfxSlider.value    = GameSettings.SfxVolume;
                if (hapticsToggle != null)  hapticsToggle.isOn = GameSettings.HapticsEnabled;
                if (fpsDropdown != null)    fpsDropdown.SetValueWithoutNotify(FpsValueToIndex(GameSettings.TargetFps));
                if (qualityDropdown != null) qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(GameSettings.QualityLevel, 0, QualityLabels.Length - 1));
            }
            finally
            {
                _suppressCallbacks = false;
            }
        }

        // -----------------------------------------------------------------------------------
        // Change handlers
        // -----------------------------------------------------------------------------------

        private void OnMasterChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.MasterVolume = v;
            GameSettings.Apply();
        }

        private void OnMusicChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.MusicVolume = v;
            GameSettings.Apply();
        }

        private void OnSfxChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.SfxVolume = v;
            GameSettings.Apply();
        }

        private void OnHapticsChanged(bool v)
        {
            if (_suppressCallbacks) return;
            GameSettings.HapticsEnabled = v;
            GameSettings.Apply();
        }

        private void OnFpsChanged(int index)
        {
            if (_suppressCallbacks) return;
            if (index < 0 || index >= FpsValues.Length) return;
            GameSettings.TargetFps = FpsValues[index];
            GameSettings.Apply();
        }

        private void OnQualityChanged(int index)
        {
            if (_suppressCallbacks) return;
            GameSettings.QualityLevel = index;
            GameSettings.Apply();
        }

        private void OnCloseClicked()
        {
            onClose?.Invoke();
        }

        // -----------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------

        private static void PopulateDropdown(TMP_Dropdown dd, IReadOnlyList<string> labels)
        {
            dd.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>(labels.Count);
            for (int i = 0; i < labels.Count; i++) options.Add(new TMP_Dropdown.OptionData(labels[i]));
            dd.AddOptions(options);
        }

        private static int FpsValueToIndex(int fps)
        {
            for (int i = 0; i < FpsValues.Length; i++)
            {
                if (FpsValues[i] == fps) return i;
            }
            // Unknown value -> default to "120 fps" slot (index 2).
            return 2;
        }

        private static void AddSliderReleaseListener(Slider slider, UnityAction onRelease)
        {
            if (slider == null) return;
            var trigger = slider.GetComponent<EventTrigger>() ?? slider.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => onRelease?.Invoke());
            trigger.triggers.Add(entry);
        }

        private void PreviewClick()
        {
            // Defensive: AudioManager may not exist in MainMenu scene if it's only spawned
            // inside the Game scene; skip silently in that case.
            if (AudioManager.Instance == null || previewClickClip == null) return;
            AudioManager.Instance.PlaySfx(previewClickClip);
        }
    }
}
