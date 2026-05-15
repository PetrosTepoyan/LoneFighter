#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.Combat.Elements.Editor
{
    /// <summary>
    /// Editor utility that scaffolds the five canonical <see cref="ElementProfile"/> ScriptableObjects
    /// plus a populated <see cref="ElementRegistry"/> asset under <c>Assets/Data/Elements/</c>.
    /// Idempotent: re-running updates existing assets rather than duplicating them.
    /// </summary>
    public static class ElementProfileGenerator
    {
        private const string OutputFolder = "Assets/Data/Elements";

        private struct Spec
        {
            public Element element;
            public string displayName;
            public Color tint;
            public string statusEffect;
        }

        // Sensible neon palette. Values >1 give a slight HDR bloom punch under URP 2D.
        private static readonly Spec[] Specs =
        {
            new Spec { element = Element.Physical,  displayName = "Physical",  tint = new Color(1.00f, 1.00f, 1.00f, 1f), statusEffect = string.Empty },
            new Spec { element = Element.Fire,      displayName = "Fire",      tint = new Color(2.20f, 0.70f, 0.10f, 1f), statusEffect = "BurnEffect" },
            new Spec { element = Element.Ice,       displayName = "Ice",       tint = new Color(0.30f, 1.60f, 2.20f, 1f), statusEffect = "ChillEffect" },
            new Spec { element = Element.Lightning, displayName = "Lightning", tint = new Color(2.30f, 2.10f, 0.20f, 1f), statusEffect = "ShockEffect" },
            new Spec { element = Element.Poison,    displayName = "Poison",    tint = new Color(0.40f, 2.00f, 0.30f, 1f), statusEffect = "PoisonEffect" },
        };

        [MenuItem("LoneFighter/Combat/Generate Element Profiles")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            var created = new List<ElementProfile>(Specs.Length);
            foreach (var spec in Specs)
            {
                var path = $"{OutputFolder}/Element_{spec.element}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<ElementProfile>(path);
                bool isNew = profile == null;
                if (isNew)
                {
                    profile = ScriptableObject.CreateInstance<ElementProfile>();
                }

                profile.EditorPopulate(spec.element, spec.displayName, spec.tint, spec.statusEffect);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(profile, path);
                }
                else
                {
                    EditorUtility.SetDirty(profile);
                }
                created.Add(profile);
            }

            // Registry asset (also idempotent).
            var registryPath = $"{OutputFolder}/ElementRegistry.asset";
            var registry = AssetDatabase.LoadAssetAtPath<ElementRegistry>(registryPath);
            bool registryNew = registry == null;
            if (registryNew)
            {
                registry = ScriptableObject.CreateInstance<ElementRegistry>();
            }
            registry.EditorSetProfiles(created);
            if (registryNew)
            {
                AssetDatabase.CreateAsset(registry, registryPath);
            }
            else
            {
                EditorUtility.SetDirty(registry);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(registry);
            Debug.Log($"[LoneFighter] Generated {created.Count} element profiles + registry at {OutputFolder}.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            // parts[0] should be "Assets".
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
            Directory.CreateDirectory(path); // belt-and-braces for fresh checkouts
        }
    }
}
#endif
