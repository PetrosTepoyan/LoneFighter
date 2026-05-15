using System;
using System.Collections.Generic;

namespace LoneFighter.Meta
{
    // ID prefix convention: "weapon:<Name>" for weapon unlocks, "achv:<Name>" for achievements.
    // The prefix lets Evaluate() route the unlocked id into the right list on SaveData.

    public struct UnlockRule
    {
        public string id;
        public string displayName;
        public string description;
        public Func<RunRecord, AllTimeStats, bool> condition;
    }

    public static class UnlockManager
    {
        private const string WeaponPrefix = "weapon:";
        private const string AchievementPrefix = "achv:";

        // Order matters only for the toast queue's natural display order.
        // Conditions read the just-finished run plus all-time totals; the all-time totals are
        // expected to have already been updated by MetaProgression *before* Evaluate is called.
        public static readonly UnlockRule[] Rules = new UnlockRule[]
        {
            new UnlockRule {
                id = WeaponPrefix + "SpreadShotgun",
                displayName = "Weapon Unlocked: Spread Shotgun",
                description = "5 projectiles in a cone — survive 60 s in any run.",
                condition = (run, _) => run != null && run.survivedSeconds >= 60f,
            },
            new UnlockRule {
                id = WeaponPrefix + "OrbitalBlade",
                displayName = "Weapon Unlocked: Orbital Blade",
                description = "Two blades orbit you — survive 120 s in any run.",
                condition = (run, _) => run != null && run.survivedSeconds >= 120f,
            },
            new UnlockRule {
                id = WeaponPrefix + "LightningChain",
                displayName = "Weapon Unlocked: Lightning Chain",
                description = "Chains to nearby enemies — survive 180 s in any run.",
                condition = (run, _) => run != null && run.survivedSeconds >= 180f,
            },
            new UnlockRule {
                id = AchievementPrefix + "FirstBlood",
                displayName = "First Blood",
                description = "Score your first kill.",
                condition = (run, stats) =>
                    (run != null && run.kills >= 1) || (stats != null && stats.totalKills >= 1),
            },
            new UnlockRule {
                id = AchievementPrefix + "Centurion",
                displayName = "Centurion",
                description = "100 kills in a single run.",
                condition = (run, _) => run != null && run.kills >= 100,
            },
            new UnlockRule {
                id = AchievementPrefix + "Survivor",
                displayName = "Survivor",
                description = "Survive a full 5-minute run.",
                condition = (run, _) => run != null && run.victory,
            },
            new UnlockRule {
                id = AchievementPrefix + "Marathoner",
                displayName = "Marathoner",
                description = "Accumulate more than 1 hour of play time.",
                condition = (_, stats) => stats != null && stats.totalPlayTimeSec > 3600f,
            },
        };

        // Evaluate all rules against the just-finished run + current all-time stats.
        // Returns rules that are now satisfied but had not previously been unlocked, and as a
        // side-effect appends their ids to the appropriate lists on SaveService.Current.
        // Caller (MetaProgression) is responsible for calling SaveService.Save() after.
        public static List<UnlockRule> Evaluate(RunRecord justFinished, AllTimeStats stats)
        {
            var newly = new List<UnlockRule>();
            var data = SaveService.Current;
            if (data == null) return newly;

            for (int i = 0; i < Rules.Length; i++)
            {
                var rule = Rules[i];
                if (IsAlreadyUnlocked(rule.id, data)) continue;
                if (rule.condition == null) continue;

                bool ok;
                try { ok = rule.condition(justFinished, stats); }
                catch { ok = false; }
                if (!ok) continue;

                Apply(rule, data);
                newly.Add(rule);
            }

            return newly;
        }

        public static bool IsAlreadyUnlocked(string id, SaveData data = null)
        {
            data ??= SaveService.Current;
            if (data == null || string.IsNullOrEmpty(id)) return false;

            if (id.StartsWith(WeaponPrefix))
            {
                string weaponName = id.Substring(WeaponPrefix.Length);
                return data.unlockedWeapons != null && data.unlockedWeapons.Contains(weaponName);
            }
            if (id.StartsWith(AchievementPrefix))
            {
                return data.unlockedAchievements != null && data.unlockedAchievements.Contains(id);
            }
            // Unknown prefix — be conservative, treat as locked so we don't lose unlocks.
            return false;
        }

        private static void Apply(UnlockRule rule, SaveData data)
        {
            if (rule.id.StartsWith(WeaponPrefix))
            {
                string weaponName = rule.id.Substring(WeaponPrefix.Length);
                if (data.unlockedWeapons == null) data.unlockedWeapons = new List<string>();
                if (!data.unlockedWeapons.Contains(weaponName)) data.unlockedWeapons.Add(weaponName);
            }
            else if (rule.id.StartsWith(AchievementPrefix))
            {
                if (data.unlockedAchievements == null) data.unlockedAchievements = new List<string>();
                if (!data.unlockedAchievements.Contains(rule.id)) data.unlockedAchievements.Add(rule.id);
            }
        }
    }
}
