using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LoneFighter.World;

namespace LoneFighter.EditorTools.World
{
    /// <summary>
    /// One-click scaffold for the world layer of a Game scene. Opens via
    /// <c>LoneFighter → World → Arena Setup</c>; the button below creates an
    /// Arena hierarchy in the active scene:
    ///
    ///   Arena
    ///     Bounds          (ArenaBounds, default 30x30)
    ///     Background      (ParallaxBackground, no layers assigned)
    ///     Hazards         (empty parent for runtime hazards)
    ///
    /// The user still has to drop sprites into the ParallaxBackground layers and
    /// (optionally) generate an arena floor via
    /// <c>LoneFighter → World → Generate Arena Background</c>.
    /// </summary>
    public class ArenaPresetWindow : EditorWindow
    {
        private Vector2 _boundsCenter = Vector2.zero;
        private Vector2 _boundsSize = new Vector2(30f, 30f);
        private bool _addStarfield = true;
        private int _starCount = 80;

        [MenuItem("LoneFighter/World/Arena Setup")]
        public static void Open()
        {
            var win = GetWindow<ArenaPresetWindow>(true, "Arena Setup", true);
            win.minSize = new Vector2(320f, 220f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Arena scaffold", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates an Arena hierarchy in the active scene with ArenaBounds + ParallaxBackground.\n" +
                "Run 'LoneFighter → World → Generate Arena Background' first if you want a floor sprite.",
                MessageType.Info);

            EditorGUILayout.Space();
            _boundsCenter = EditorGUILayout.Vector2Field("Bounds Center", _boundsCenter);
            _boundsSize = EditorGUILayout.Vector2Field("Bounds Size", _boundsSize);

            EditorGUILayout.Space();
            _addStarfield = EditorGUILayout.Toggle("Include Starfield", _addStarfield);
            using (new EditorGUI.DisabledScope(!_addStarfield))
            {
                _starCount = EditorGUILayout.IntSlider("Star Count", _starCount, 10, 300);
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Create Arena in Active Scene", GUILayout.Height(32f)))
            {
                CreateArena();
            }
        }

        private void CreateArena()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Arena Setup", "No active scene found. Open or create a scene first.", "OK");
                return;
            }

            // Reuse an existing 'Arena' root if the user runs this twice rather than duplicating it.
            var existing = GameObject.Find("Arena");
            var arena = existing != null ? existing : new GameObject("Arena");
            Undo.RegisterCreatedObjectUndo(arena, "Create Arena");

            var bounds = EnsureChild(arena.transform, "Bounds");
            var boundsComp = bounds.GetComponent<ArenaBounds>();
            if (boundsComp == null) boundsComp = Undo.AddComponent<ArenaBounds>(bounds);

            // Apply window-configured size via SerializedObject so values persist & undo correctly.
            var so = new SerializedObject(boundsComp);
            so.FindProperty("center").vector2Value = _boundsCenter;
            so.FindProperty("size").vector2Value = _boundsSize;
            so.ApplyModifiedPropertiesWithoutUndo();

            var bg = EnsureChild(arena.transform, "Background");
            if (bg.GetComponent<ParallaxBackground>() == null)
                Undo.AddComponent<ParallaxBackground>(bg);

            if (_addStarfield)
            {
                var stars = EnsureChild(bg.transform, "Starfield");
                var sf = stars.GetComponent<StarfieldBackground>();
                if (sf == null) sf = Undo.AddComponent<StarfieldBackground>(stars);
                var sso = new SerializedObject(sf);
                var countProp = sso.FindProperty("starCount");
                if (countProp != null) countProp.intValue = _starCount;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureChild(arena.transform, "Hazards");

            Selection.activeGameObject = arena;
            EditorSceneManager.MarkSceneDirty(arena.scene);

            Debug.Log(
                "[LoneFighter.World] Arena scaffold created:\n" +
                $"  • Arena (root)\n" +
                $"  • Arena/Bounds (ArenaBounds, center {_boundsCenter}, size {_boundsSize})\n" +
                $"  • Arena/Background (ParallaxBackground — assign sprite layers manually)\n" +
                (_addStarfield ? $"  • Arena/Background/Starfield (StarfieldBackground, {_starCount} stars)\n" : string.Empty) +
                "  • Arena/Hazards (empty parent — runtime hazards get spawned underneath)\n" +
                "Next: run 'LoneFighter → World → Generate Arena Background' for a floor sprite, then drop it on a Tiled SpriteRenderer under Arena/Background."
            );
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Undo.SetTransformParent(go.transform, parent, "Parent " + name);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }
    }
}
