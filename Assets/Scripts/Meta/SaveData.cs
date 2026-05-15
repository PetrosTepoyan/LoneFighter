using System;
using System.Collections.Generic;

namespace LoneFighter.Meta
{
    // All persistence-layer POCOs live here. JsonUtility serializes [Serializable] classes
    // with public fields; it does NOT serialize dictionaries, so everything is List<T>-shaped.
    //
    // Read paths must be defensive: a save written by an older build may have missing
    // sub-objects / lists. Constructors + EnsureDefaults() backfill any null reference so
    // the rest of the game can treat fields as non-null.

    [Serializable]
    public class SaveData
    {
        public int saveVersion = 1;
        public PlayerProfile profile;
        public List<RunRecord> runHistory;
        public AllTimeStats totals;
        public List<string> unlockedWeapons;
        public List<string> unlockedAchievements;
        public Settings settings;

        public SaveData()
        {
            EnsureDefaults();
        }

        // Called both by the default ctor and after JsonUtility deserialization, where any
        // missing field comes back as null. Cheap and idempotent.
        public void EnsureDefaults()
        {
            if (saveVersion < 1) saveVersion = 1;
            if (profile == null) profile = new PlayerProfile();
            profile.EnsureDefaults();

            if (runHistory == null) runHistory = new List<RunRecord>();
            if (totals == null) totals = new AllTimeStats();

            if (unlockedWeapons == null) unlockedWeapons = new List<string>();
            // Pistol is the starter weapon; it is always unlocked.
            if (!unlockedWeapons.Contains("Pistol")) unlockedWeapons.Add("Pistol");

            if (unlockedAchievements == null) unlockedAchievements = new List<string>();

            if (settings == null) settings = new Settings();
            settings.EnsureDefaults();
        }
    }

    [Serializable]
    public class PlayerProfile
    {
        public string nickname;
        public long createdAtUnixSec;
        public int runsStarted;
        public int runsWon;

        public void EnsureDefaults()
        {
            if (string.IsNullOrEmpty(nickname)) nickname = "Lone Fighter";
            if (createdAtUnixSec <= 0)
            {
                createdAtUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }
    }

    [Serializable]
    public class RunRecord
    {
        public long timestampUnixSec;
        public float survivedSeconds;
        public int kills;
        public int peakLevel;
        public bool victory;
        public string deathCause;
        public List<string> weaponsCarried;
        public List<string> upgradesPicked;

        public RunRecord()
        {
            weaponsCarried = new List<string>();
            upgradesPicked = new List<string>();
            deathCause = string.Empty;
        }
    }

    [Serializable]
    public class AllTimeStats
    {
        public int totalKills;
        public float totalPlayTimeSec;
        public int totalRuns;
        public int totalVictories;
        public float longestSurvival;
        public int bestKillStreak;
    }

    [Serializable]
    public class Settings
    {
        public float masterVolume;
        public float musicVolume;
        public float sfxVolume;
        public bool hapticsEnabled;
        public int targetFps;
        public int qualityLevel;
        public string locale;

        // A sentinel so we can distinguish "JsonUtility wrote a zero default" from "the user
        // actually set this". We bump it once on creation; if it stays 0 after a load (legacy
        // / partial save), we restore defaults.
        public int _settingsInitialized;

        public void EnsureDefaults()
        {
            if (_settingsInitialized == 0)
            {
                masterVolume = 0.8f;
                musicVolume = 0.5f;
                sfxVolume = 0.8f;
                hapticsEnabled = true;
                targetFps = 120;
                qualityLevel = 1;
                locale = "en";
                _settingsInitialized = 1;
            }
            if (string.IsNullOrEmpty(locale)) locale = "en";
            if (targetFps <= 0) targetFps = 120;
        }
    }
}
