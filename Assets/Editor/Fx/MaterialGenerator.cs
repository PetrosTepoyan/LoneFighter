using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoneFighter.EditorTools.Fx
{
    /// Generates the shared additive HDR material used by every Fx_* particle prefab.
    /// Driven by FxPrefabGenerator (which calls GetOrCreate) but also exposed as its own menu item
    /// so users can regenerate just the material without touching prefabs.
    public static class MaterialGenerator
    {
        // URP "Particles/Unlit" shader property reference (URP 17.x):
        //   _Surface           0 = Opaque, 1 = Transparent
        //   _Blend             0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
        //   _SrcBlend, _DstBlend, _ZWrite — the actual blend equation (we set these manually for true additive)
        //   _BaseMap, _BaseColor — sprite + tint (HDR)
        //
        // Keyword toggles relevant to particles shader (set via material.EnableKeyword / DisableKeyword):
        //   _SURFACE_TYPE_TRANSPARENT  — required for transparent surface
        //   _ALPHAPREMULTIPLY_ON       — premultiplied alpha (we leave OFF for straight additive)
        //   _ALPHAMODULATE_ON          — soft-particle alpha modulation (off here)
        //   _EMISSION                  — enables _EmissionColor (we don't need it; HDR _BaseColor + Bloom does the work)
        // Render queue is forced to Transparent (3000) so additive particles draw after opaque geometry.

        [MenuItem("LoneFighter/FX/Generate Additive Material", priority = 22)]
        public static void GenerateMenu()
        {
            var mat = GetOrCreate(forceRebuild: true);
            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);
            Debug.Log($"[LoneFighter.FX] Additive material ready at {FxAssetPaths.AdditiveMatPath}");
        }

        /// Returns the additive material, creating + saving it on first call.
        /// Set forceRebuild=true to overwrite an existing one (used when the user re-runs the menu).
        public static Material GetOrCreate(bool forceRebuild = false)
        {
            FxAssetPaths.EnsureFolder(FxAssetPaths.SettingsDir);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(FxAssetPaths.AdditiveMatPath);
            if (existing != null && !forceRebuild) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[LoneFighter.FX] Could not find shader 'Universal Render Pipeline/Particles/Unlit'. " +
                               "Is the URP package installed?");
                return null;
            }

            Material mat;
            if (existing != null)
            {
                mat = existing;
                mat.shader = shader;
            }
            else
            {
                mat = new Material(shader) { name = "Fx_Additive" };
            }

            ConfigureAdditive(mat);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mat, FxAssetPaths.AdditiveMatPath);
            }
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            return mat;
        }

        private static void ConfigureAdditive(Material mat)
        {
            // Surface = Transparent
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            // _Blend = 2 (Additive) is the URP-inspector dropdown value. We also set the explicit blend factors
            // because the dropdown only writes them when the inspector runs OnValidate — we won't run that here.
            if (mat.HasProperty("_Blend"))     mat.SetFloat("_Blend", 2f);
            if (mat.HasProperty("_SrcBlend"))  mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlend"))  mat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (mat.HasProperty("_ZWrite"))    mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull"))      mat.SetFloat("_Cull", (float)CullMode.Off);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent; // 3000

            // Keywords for URP particle Unlit. We want straight-additive — no premultiply, no alpha-modulate.
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Soft particles can be expensive on mobile; leave them off by default.
            mat.DisableKeyword("_SOFTPARTICLES_ON");

            // HDR white base color — actual brightness pop comes from each particle's startColor (also HDR).
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", Color.white);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", Color.white);
            }

            // Base map = our soft circle. This is the sprite each particle uses.
            var tex = FxSoftCircleTexture.GetOrCreate();
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
        }
    }
}
