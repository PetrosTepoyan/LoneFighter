using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LoneFighter.EditorTools.Fx
{
    /// Generates the URP VolumeProfile that drives the "expensive" look:
    /// Bloom + Vignette + Chromatic Aberration + Color Adjustments. The profile is the asset that
    /// the user then drags onto a Global Volume in Game.unity.
    public static class PostProcessingGenerator
    {
        [MenuItem("LoneFighter/FX/Generate Post-Processing Profile", priority = 21)]
        public static void Generate()
        {
            FxAssetPaths.EnsureFolder(FxAssetPaths.SettingsDir);

            // Create or load the profile.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FxAssetPaths.PostProfilePath);
            bool isNew = profile == null;
            if (isNew)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, FxAssetPaths.PostProfilePath);
            }
            else
            {
                // Strip existing overrides so re-running the menu is idempotent.
                for (int i = profile.components.Count - 1; i >= 0; i--)
                {
                    var c = profile.components[i];
                    profile.components.RemoveAt(i);
                    Object.DestroyImmediate(c, true);
                }
            }

            // --- Bloom ---
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value         = 0.9f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value         = 1.0f;
            bloom.scatter.overrideState   = true;
            bloom.scatter.value           = 0.7f;
            bloom.tint.overrideState      = true;
            bloom.tint.value              = Color.white;

            // --- Vignette ---
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState  = true;
            vignette.intensity.value          = 0.25f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value         = 0.4f;
            vignette.color.overrideState      = true;
            vignette.color.value              = Color.black;

            // --- Chromatic Aberration ---
            var chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.overrideState = true;
            chroma.intensity.value         = 0.15f;

            // --- Color Adjustments ---
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.overrideState = true;
            color.postExposure.value         = 0.2f;
            color.saturation.overrideState   = true;
            color.saturation.value           = 15f;
            color.contrast.overrideState     = true;
            color.contrast.value             = 5f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);

            Debug.Log("[LoneFighter.FX] Post-processing profile " +
                      (isNew ? "created" : "rebuilt") + " at " + FxAssetPaths.PostProfilePath +
                      "\nNext step: in Game.unity, add a Global Volume (Component > Volume > Global Volume) and drag this profile into its Profile field. " +
                      "Also confirm URP Asset > Post Processing is enabled and HDR is on.");
        }
    }
}
