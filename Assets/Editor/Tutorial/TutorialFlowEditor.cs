#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LoneFighter.Tutorial;

namespace LoneFighter.EditorTools.Tutorial
{
    /// <summary>
    /// Editor utility window for managing tutorial / onboarding progress while
    /// testing. Exposes two menu items under <c>LoneFighter → Tutorial</c>
    /// for quick wipe / fast-forward, plus a small EditorWindow listing the
    /// default steps with per-step buttons.
    /// </summary>
    public class TutorialFlowEditor : EditorWindow
    {
        [MenuItem("LoneFighter/Tutorial/Reset Tutorial")]
        public static void ResetTutorial()
        {
            TutorialState.ResetAll();
            Debug.Log("[LoneFighter] Tutorial state wiped. Next run will be treated as first-ever.");
        }

        [MenuItem("LoneFighter/Tutorial/Mark All Completed")]
        public static void MarkAllCompleted()
        {
            // Includes the bootstrap marker so IsFirstEverRun returns false.
            TutorialState.MarkCompleted(TutorialState.BootstrapStepId);
            foreach (var step in TutorialManager.DefaultSteps())
            {
                if (step == null || string.IsNullOrEmpty(step.id)) continue;
                TutorialState.MarkCompleted(step.id);
            }
            Debug.Log("[LoneFighter] All tutorial steps marked completed. Next run will skip onboarding.");
        }

        [MenuItem("LoneFighter/Tutorial/Open Flow Window")]
        public static void OpenWindow()
        {
            var win = GetWindow<TutorialFlowEditor>(false, "Tutorial Flow", true);
            win.minSize = new Vector2(360f, 240f);
            win.Show();
        }

        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tutorial / Onboarding", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use these buttons to wipe or fast-forward the player's persisted tutorial state. " +
                "Backed by PlayerPrefs key '" + TutorialState.PrefsKey + "'.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset All", GUILayout.Height(28))) ResetTutorial();
                if (GUILayout.Button("Mark All Completed", GUILayout.Height(28))) MarkAllCompleted();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("First-ever run:", TutorialState.IsFirstEverRun ? "YES" : "no");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default steps", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var step in TutorialManager.DefaultSteps())
            {
                if (step == null) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool done = TutorialState.IsCompleted(step.id);
                    EditorGUILayout.LabelField($"{step.id}  —  {(done ? "DONE" : "pending")}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(step.headline, EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(done ? "Re-arm" : "Mark Done"))
                        {
                            if (done)
                            {
                                // No granular un-mark API by design; use ResetAll
                                // and reapply the others.
                                TutorialState.ResetAll();
                                foreach (var other in TutorialManager.DefaultSteps())
                                {
                                    if (other == null || other.id == step.id) continue;
                                    if (TutorialState.IsCompleted(other.id))
                                        TutorialState.MarkCompleted(other.id);
                                }
                            }
                            else
                            {
                                TutorialState.MarkCompleted(step.id);
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
