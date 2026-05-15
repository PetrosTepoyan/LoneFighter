using LoneFighter.Systems;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.EditorTools.Fx
{
    /// Generates the camera + FX rig prefabs that complement the FX prefabs:
    ///   - CM_PlayerFollow.prefab : a Cinemachine 3 CinemachineCamera with CinemachineFollow.
    ///   - FxRig.prefab           : an empty GameObject with FxService + CinemachineImpulseSource.
    ///
    /// Note: CinemachineBrain belongs on the Main Camera in the scene, not in a prefab — the user
    /// adds that once when they wire up Game.unity (see SETUP.md / VFX.md).
    public static class CinemachineRigGenerator
    {
        [MenuItem("LoneFighter/FX/Generate Cinemachine Rig", priority = 23)]
        public static void Generate()
        {
            FxAssetPaths.EnsureFolder(FxAssetPaths.PrefabsDir);
            FxAssetPaths.EnsureFolder(FxAssetPaths.PrefabsFxDir);

            BuildCinemachineCamera();
            BuildFxRig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LoneFighter.FX] Generated camera + FX rig prefabs.\n" +
                      "Next steps:\n" +
                      "  1. Drop " + FxAssetPaths.CmPlayerFollowPath + " into Game.unity and set its Tracking Target to the Player.\n" +
                      "  2. Drop " + FxAssetPaths.FxRigPrefabPath + " into Game.unity under Systems and wire the Fx_*.prefabs into its serialized fields.\n" +
                      "  3. Add CinemachineBrain to Main Camera (Component > Cinemachine > CinemachineBrain) and a CinemachineImpulseListener for screen shake.");
        }

        // ---------------------------------------------------------------------

        private static void BuildCinemachineCamera()
        {
            var go = new GameObject("CM_PlayerFollow");

            var cam = go.AddComponent<CinemachineCamera>();
            // Orthographic lens for 2D, size 6 matches Main Camera in SETUP.md step 13.
            // In Cinemachine 3.x, LensSettings.Orthographic is a get-only mirror of the brain camera;
            // ModeOverride is the writable knob that forces orthographic on the virtual camera.
            var lens = cam.Lens;
            lens.ModeOverride     = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = 6f;
            lens.NearClipPlane    = -100f;
            lens.FarClipPlane     = 100f;
            cam.Lens = lens;

            // Follow behavior with light damping so the camera trails the player smoothly.
            var follow = go.AddComponent<CinemachineFollow>();
            follow.TrackerSettings.PositionDamping = new Vector3(0.3f, 0.3f, 0f);

            PrefabUtility.SaveAsPrefabAsset(go, FxAssetPaths.CmPlayerFollowPath);
            Object.DestroyImmediate(go);
        }

        private static void BuildFxRig()
        {
            var go = new GameObject("FxRig");
            // FxService — user still needs to drag the Fx_*.prefabs and damage popup prefab into the
            // serialized slots. We intentionally leave those references unwired (see VFX.md).
            go.AddComponent<FxService>();

            // ImpulseSource for camera shake. FxService.LightShake/HeavyShake call GenerateImpulse on this.
            go.AddComponent<CinemachineImpulseSource>();

            PrefabUtility.SaveAsPrefabAsset(go, FxAssetPaths.FxRigPrefabPath);
            Object.DestroyImmediate(go);
        }
    }
}
