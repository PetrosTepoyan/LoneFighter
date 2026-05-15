using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using LoneFighter.Effects.CriticalHits;

namespace LoneFighter.Effects.CriticalHits.EditorTools
{
    /// <summary>
    /// One-click prefab generator for the CRIT damage popup.
    ///
    /// Builds a TextMeshPro-based GameObject with a <see cref="CritDamagePopup"/>
    /// component and saves it to <c>Assets/Prefabs/UI/CritDamagePopup.prefab</c>
    /// via <see cref="PrefabUtility.SaveAsPrefabAsset"/>. Re-running the menu is
    /// idempotent — the existing prefab is overwritten in place so any external
    /// references survive.
    ///
    /// Why a generator instead of hand-authoring? Same rationale as
    /// <c>FxPrefabGenerator</c>: keeps the prefab definition in version-controlled
    /// source so a clean checkout can rebuild every asset.
    /// </summary>
    public static class CritPopupPrefabGenerator
    {
        private const string PrefabDir = "Assets/Prefabs/UI";
        private const string PrefabPath = PrefabDir + "/CritDamagePopup.prefab";

        [MenuItem("LoneFighter/FX/Generate Crit Popup Prefab", priority = 30)]
        public static void Generate()
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("CritDamagePopup");
            try
            {
                // World-space TextMeshPro renders cleanly through a 2D camera,
                // ignores Canvas hierarchy, and works with PoolService out of the box.
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.text = "0 CRIT!";
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 6f; // world units; larger than the standard popup
                tmp.enableAutoSizing = false;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = new Color(1f, 0.95f, 0.3f, 1f); // yellow start state
                // Render above gameplay sprites — matches the convention used by
                // the FX particle prefabs (sortingOrder = 50/60).
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sortingLayerID = SortingLayer.NameToID("Default");
                    mr.sortingOrder = 100;
                }

                // Component does the work — defaults pulled from CritDamagePopup
                // builder methods are serialized automatically when the prefab is saved.
                go.AddComponent<CritDamagePopup>();

                // Start hidden; popup turns itself on in OnEnable when pooled.
                go.transform.localScale = Vector3.zero;

                // SaveAsPrefabAsset returns the saved asset (and overwrites if it exists).
                var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out bool success);
                if (!success || saved == null)
                {
                    Debug.LogError($"[LoneFighter.CritFx] Failed to save prefab at {PrefabPath}");
                    return;
                }

                Debug.Log($"[LoneFighter.CritFx] Wrote {PrefabPath}");
            }
            finally
            {
                // Always clean up the scene-side template object, even on early returns.
                Object.DestroyImmediate(go);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// Mirrors the folder-bootstrap pattern used by <c>FxAssetPaths.EnsureFolder</c>:
        /// recursively create any missing parent folders so AssetDatabase.CreateFolder
        /// doesn't fail on a fresh checkout that lacks <c>Assets/Prefabs/UI</c>.
        /// </summary>
        private static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath)) return;
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
