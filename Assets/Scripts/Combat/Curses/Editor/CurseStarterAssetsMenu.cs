// CurseStarterAssetsMenu.cs — one-click generator that materializes the
// eight starter curses as ScriptableObject assets under Assets/Data/Curses/.
//
// Idempotent: re-running updates the existing asset's fields in place so
// any references the user has wired in scenes survive across re-generates.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.Combat.Curses.EditorTools
{
    /// <summary>
    /// Editor-only menu item that creates / updates the eight starter
    /// <see cref="Curse"/> assets used by the curse system.
    /// </summary>
    public static class CurseStarterAssetsMenu
    {
        private const string OutputFolder = "Assets/Data/Curses";

        // Default field values for each starter curse. Kept as a private
        // struct so we don't expose a parallel data shape outside the menu.
        private readonly struct Starter
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly CurseKind Kind;
            public readonly float Magnitude;
            public readonly float RewardBonus;

            public Starter(string id, string name, string description, CurseKind kind, float magnitude, float rewardBonus)
            {
                Id = id;
                DisplayName = name;
                Description = description;
                Kind = kind;
                Magnitude = magnitude;
                RewardBonus = rewardBonus;
            }
        }

        // Numbers chosen to match the brief:
        //   * Bloodlust: +30% enemy damage
        //   * Swarm: +50% spawn rate (1.5x)
        //   * Fragile: -25% max HP
        //   * Slowed: -15% move speed
        //   * NoCrits: flag, no magnitude
        //   * GlassCannon: +50% damage / -50% HP
        //   * DenseFog: flag, no magnitude
        //   * ExtraBosses: 2x boss count (magnitude not used by handler)
        private static readonly Starter[] Starters =
        {
            new Starter("curse_bloodlust",    "Bloodlust",     "Enemies deal +{magnitude} contact damage.",                   CurseKind.Bloodlust,   0.30f, 0.20f),
            new Starter("curse_swarm",        "Swarm",         "Enemies spawn {magnitude} more often.",                       CurseKind.Swarm,       0.50f, 0.25f),
            new Starter("curse_fragile",      "Fragile",       "You start with {magnitude} less max HP.",                     CurseKind.Fragile,     0.25f, 0.20f),
            new Starter("curse_slowed",       "Slowed",        "You move {magnitude} slower.",                                CurseKind.Slowed,      0.15f, 0.15f),
            new Starter("curse_no_crits",     "No Crits",      "Your attacks can no longer critically strike.",               CurseKind.NoCrits,     0f,    0.20f),
            new Starter("curse_glass_cannon", "Glass Cannon",  "Deal {magnitude} more damage, but lose {magnitude} of your max HP.", CurseKind.GlassCannon, 0.50f, 0.30f),
            new Starter("curse_dense_fog",    "Dense Fog",     "A heavy fog limits your view of the arena.",                  CurseKind.DenseFog,    0f,    0.15f),
            new Starter("curse_extra_bosses", "Extra Bosses",  "Twice as many bosses spawn during the run.",                  CurseKind.ExtraBosses, 0f,    0.35f),
        };

        [MenuItem("LoneFighter/Curses/Generate Starter Curses")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            int created = 0;
            int updated = 0;

            for (int i = 0; i < Starters.Length; i++)
            {
                var s = Starters[i];
                string path = $"{OutputFolder}/{ToFileName(s.DisplayName)}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Curse>(path);
                bool isNew = existing == null;
                if (isNew)
                {
                    existing = ScriptableObject.CreateInstance<Curse>();
                    AssetDatabase.CreateAsset(existing, path);
                    created++;
                }
                else
                {
                    updated++;
                }

                existing.id = s.Id;
                existing.displayName = s.DisplayName;
                existing.description = s.Description;
                existing.kind = s.Kind;
                existing.magnitude = s.Magnitude;
                existing.rewardBonus = s.RewardBonus;
                // We deliberately do NOT overwrite the icon. Designers are
                // expected to drag sprites in by hand; clobbering on re-run
                // would lose their wiring.

                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CurseStarterAssetsMenu] Done. Created {created}, updated {updated} curse assets under {OutputFolder}.");
            EditorUtility.DisplayDialog(
                "Curses",
                $"Generated starter curses.\n\nCreated: {created}\nUpdated: {updated}\n\nLocation: {OutputFolder}",
                "OK");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            // Walk parents in order, creating each missing segment.
            var parts = folder.Split('/');
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

        private static string ToFileName(string displayName)
        {
            // Strip spaces and non-alphanumerics so the asset file matches
            // the existing convention used by ContentSeederWindow.
            if (string.IsNullOrEmpty(displayName)) return "Curse";
            var sb = new System.Text.StringBuilder(displayName.Length);
            for (int i = 0; i < displayName.Length; i++)
            {
                char c = displayName[i];
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.Length == 0 ? "Curse" : sb.ToString();
        }
    }
}
#endif
