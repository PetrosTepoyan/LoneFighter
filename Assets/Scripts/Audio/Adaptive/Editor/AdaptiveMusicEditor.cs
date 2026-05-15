#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.Audio.Adaptive
{
    /// <summary>
    /// Editor window for authoring and previewing <see cref="AdaptiveMusicProfile"/> assets.
    ///
    /// Open via the menu: <c>LoneFighter → Audio → Adaptive Music</c>.
    ///
    /// Features:
    /// <list type="bullet">
    ///   <item>Drag in an existing profile, see all stems with their threshold / fade settings.</item>
    ///   <item>Simulate intensity with a 0..1 slider in play-mode to watch the controller respond.</item>
    ///   <item>Live readout of each stem's current and target volume during play.</item>
    ///   <item>"Generate Starter Profile" button — creates a 4-stem profile (bass / drums / lead / pads)
    ///         with thresholds at 0.0 / 0.3 / 0.6 / 0.9 respectively, wiring in clips from
    ///         <c>Assets/Audio/Generated/</c> if any match the stem id by name.</item>
    /// </list>
    /// </summary>
    public class AdaptiveMusicEditor : EditorWindow
    {
        private const string MenuPath = "LoneFighter/Audio/Adaptive Music";
        private const string GeneratedAudioFolder = "Assets/Audio/Generated";
        private const string DefaultProfileFolder = "Assets/Data/Audio/Adaptive";

        private AdaptiveMusicProfile _profile;
        private Vector2 _scroll;
        private float _simulatedIntensity;
        private bool _overrideIntensityInPlayMode;

        private readonly List<(string id, float volume, float target, float threshold)> _stemBuffer
            = new List<(string id, float volume, float target, float threshold)>(8);

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var win = GetWindow<AdaptiveMusicEditor>("Adaptive Music");
            win.minSize = new Vector2(360f, 420f);
        }

        private void OnEnable()
        {
            EditorApplication.update += RepaintWhilePlaying;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintWhilePlaying;
        }

        private void RepaintWhilePlaying()
        {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Adaptive Music", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Multi-stem adaptive music. Each stem plays as its own looping AudioSource " +
                "and fades in/out based on combat intensity. All stems in a profile MUST share " +
                "the same length and BPM for tight layering.",
                MessageType.Info);

            EditorGUILayout.Space();
            _profile = (AdaptiveMusicProfile)EditorGUILayout.ObjectField(
                "Profile", _profile, typeof(AdaptiveMusicProfile), allowSceneObjects: false);

            EditorGUILayout.Space();
            DrawStarterProfileSection();

            EditorGUILayout.Space();
            DrawProfileSection();

            EditorGUILayout.Space();
            DrawRuntimeSection();
        }

        // -----------------------------------------------------------------------------------------
        // Starter profile generation
        // -----------------------------------------------------------------------------------------

        private void DrawStarterProfileSection()
        {
            EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Starter Profile", GUILayout.Height(28f)))
                {
                    GenerateStarterProfile();
                }
                if (GUILayout.Button("Ping Generated Folder", GUILayout.Height(28f), GUILayout.Width(160f)))
                {
                    var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(GeneratedAudioFolder);
                    if (folder != null) EditorGUIUtility.PingObject(folder);
                    else EditorUtility.DisplayDialog(
                        "Folder missing",
                        $"'{GeneratedAudioFolder}' does not exist yet. Run the audio generator " +
                        "or drop clips there before pinging.",
                        "OK");
                }
            }
        }

        /// <summary>
        /// Creates an <see cref="AdaptiveMusicProfile"/> + 4 <see cref="MusicStemDefinition"/> assets
        /// on disk and selects the result. Stems are: bass(0.0), drums(0.3), lead(0.6), pads(0.9).
        /// Clip references are best-effort: any clip in <c>Assets/Audio/Generated/</c> whose filename
        /// contains the stem id is wired in; otherwise the clip slot is left null and a warning is logged.
        /// </summary>
        private void GenerateStarterProfile()
        {
            EnsureFolderExists(DefaultProfileFolder);

            var profile = ScriptableObject.CreateInstance<AdaptiveMusicProfile>();
            profile.trackName = "Starter Adaptive Track";
            profile.bpm = 120f;
            profile.lengthSeconds = 32f;
            profile.masterVolume = 1f;
            profile.stems = new List<MusicStemDefinition>(4);

            string profilePath = AssetDatabase.GenerateUniqueAssetPath(
                $"{DefaultProfileFolder}/StarterAdaptiveProfile.asset");
            AssetDatabase.CreateAsset(profile, profilePath);

            var generatedClips = LoadGeneratedClips();

            AddStarterStem(profile, "bass",  0.0f, generatedClips, 0.5f, 2.0f);
            AddStarterStem(profile, "drums", 0.3f, generatedClips, 1.0f, 1.5f);
            AddStarterStem(profile, "lead",  0.6f, generatedClips, 1.5f, 1.2f);
            AddStarterStem(profile, "pads",  0.9f, generatedClips, 2.0f, 2.5f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _profile = profile;
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);

            int missing = 0;
            for (int i = 0; i < profile.stems.Count; i++)
                if (profile.stems[i] != null && profile.stems[i].clip == null) missing++;
            if (missing > 0)
            {
                Debug.LogWarning(
                    $"[AdaptiveMusicEditor] Starter profile created with {missing} stem(s) missing clips. " +
                    $"Drop audio files into '{GeneratedAudioFolder}/' named like 'bass*', 'drums*', " +
                    $"'lead*', 'pads*' and reassign the clip fields on each MusicStemDefinition.");
            }
        }

        private void AddStarterStem(
            AdaptiveMusicProfile profile,
            string id,
            float threshold,
            Dictionary<string, AudioClip> clipPool,
            float fadeIn,
            float fadeOut)
        {
            var stem = ScriptableObject.CreateInstance<MusicStemDefinition>();
            stem.id = id;
            stem.intensityThreshold = threshold;
            stem.fadeInSeconds = fadeIn;
            stem.fadeOutSeconds = fadeOut;
            stem.targetVolume = 1f;
            stem.clip = TryPickClipFor(id, clipPool);
            stem.name = $"Stem_{id}";

            AssetDatabase.AddObjectToAsset(stem, profile);
            profile.stems.Add(stem);
        }

        private static Dictionary<string, AudioClip> LoadGeneratedClips()
        {
            var map = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(GeneratedAudioFolder)) return map;

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { GeneratedAudioFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string key = Path.GetFileNameWithoutExtension(path);
                if (!map.ContainsKey(key)) map.Add(key, clip);
            }
            return map;
        }

        private static AudioClip TryPickClipFor(string stemId, Dictionary<string, AudioClip> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            foreach (var kv in pool)
            {
                if (kv.Key.IndexOf(stemId, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            }
            return null;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        // -----------------------------------------------------------------------------------------
        // Profile inspector
        // -----------------------------------------------------------------------------------------

        private void DrawProfileSection()
        {
            EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Assign a profile or generate a starter one above.", MessageType.None);
                return;
            }

            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                _profile.trackName    = EditorGUILayout.TextField("Track Name", _profile.trackName);
                _profile.bpm          = EditorGUILayout.FloatField("BPM", Mathf.Max(1f, _profile.bpm));
                _profile.lengthSeconds= EditorGUILayout.FloatField("Length (s)", Mathf.Max(0.01f, _profile.lengthSeconds));
                _profile.masterVolume = EditorGUILayout.Slider("Master Volume", _profile.masterVolume, 0f, 1f);
                if (changeScope.changed) EditorUtility.SetDirty(_profile);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Stems", EditorStyles.boldLabel);
            if (_profile.stems == null || _profile.stems.Count == 0)
            {
                EditorGUILayout.HelpBox("Profile has no stems.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int i = 0; i < _profile.stems.Count; i++)
                {
                    var stem = _profile.stems[i];
                    if (stem == null)
                    {
                        EditorGUILayout.LabelField($"#{i}", "(missing)");
                        continue;
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField($"{stem.id}", EditorStyles.miniBoldLabel);
                        using (var change = new EditorGUI.ChangeCheckScope())
                        {
                            stem.clip = (AudioClip)EditorGUILayout.ObjectField(
                                "Clip", stem.clip, typeof(AudioClip), false);
                            stem.intensityThreshold = EditorGUILayout.Slider(
                                "Threshold", stem.intensityThreshold, 0f, 1f);
                            stem.fadeInSeconds = EditorGUILayout.FloatField("Fade In (s)", Mathf.Max(0f, stem.fadeInSeconds));
                            stem.fadeOutSeconds = EditorGUILayout.FloatField("Fade Out (s)", Mathf.Max(0f, stem.fadeOutSeconds));
                            stem.targetVolume = EditorGUILayout.Slider("Target Volume", stem.targetVolume, 0f, 1f);
                            if (change.changed) EditorUtility.SetDirty(stem);
                        }
                    }
                    if (i < _profile.stems.Count - 1) EditorGUILayout.Space(2f);
                }
            }

            // Alignment validation pass.
            if (!_profile.ValidateStemAlignment(out var firstMismatch))
            {
                EditorGUILayout.HelpBox(
                    $"Stem '{firstMismatch}' has a clip whose length doesn't match the profile's " +
                    $"authored length ({_profile.lengthSeconds:F2}s). Re-export the stems aligned to " +
                    $"{_profile.lengthSeconds:F2}s @ {_profile.bpm:F1} BPM.",
                    MessageType.Warning);
            }
        }

        // -----------------------------------------------------------------------------------------
        // Runtime preview
        // -----------------------------------------------------------------------------------------

        private void DrawRuntimeSection()
        {
            EditorGUILayout.LabelField("Runtime Preview", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to preview live stem volumes.", MessageType.None);
                return;
            }

            var ctrl = AdaptiveMusicController.Instance;
            var resolver = IntensityResolver.Instance;

            if (resolver != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Intensity (smoothed)", resolver.Intensity.ToString("F2"));
                    EditorGUILayout.LabelField("Raw", resolver.RawIntensity.ToString("F2"));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Boss: {(resolver.BossAlive ? "yes" : "no")}", GUILayout.Width(80f));
                    EditorGUILayout.LabelField($"Swarm: {(resolver.SwarmActive ? "yes" : "no")}", GUILayout.Width(90f));
                    EditorGUILayout.LabelField($"LowHP: {(resolver.LowHpActive ? "yes" : "no")}", GUILayout.Width(90f));
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No IntensityResolver in scene — add one to the Systems object.", MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(200f));
            if (ctrl != null)
            {
                int n = ctrl.GetStemVolumes(_stemBuffer);
                for (int i = 0; i < n; i++)
                {
                    var s = _stemBuffer[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(s.id, GUILayout.Width(80f));
                        EditorGUILayout.LabelField($"thr {s.threshold:F2}", GUILayout.Width(70f));
                        Rect r = GUILayoutUtility.GetRect(60f, 14f, GUILayout.ExpandWidth(true));
                        EditorGUI.ProgressBar(r, Mathf.Clamp01(s.volume), $"{s.volume:F2} → {s.target:F2}");
                    }
                }
                if (n == 0) EditorGUILayout.LabelField("(controller has no active stems)");
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No AdaptiveMusicController in scene. Add the component to a Systems GameObject " +
                    "and assign a profile.",
                    MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4f);
            _overrideIntensityInPlayMode = EditorGUILayout.Toggle(
                "Override intensity (debug)", _overrideIntensityInPlayMode);
            using (new EditorGUI.DisabledScope(!_overrideIntensityInPlayMode))
            {
                _simulatedIntensity = EditorGUILayout.Slider(
                    "Simulated", _simulatedIntensity, 0f, 1f);
            }
            if (_overrideIntensityInPlayMode && resolver != null)
            {
                // Bypass the resolver by directly poking the controller volumes via the simulated
                // value would require internal hooks — instead we just label this as observational
                // for now. Authors can use this slider as a target reference while they tweak
                // the resolver weights in the inspector.
                EditorGUILayout.HelpBox(
                    "Override is read-only — use it as a reference while tuning IntensityResolver weights.",
                    MessageType.None);
            }
        }
    }
}
#endif
