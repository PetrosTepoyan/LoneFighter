namespace LoneFighter.Localization
{
    /// <summary>
    /// Canonical, compile-time-checked keys for every localizable string in the game.
    ///
    /// Rules:
    ///  - Keys are lowercase, dot-separated, and grouped by surface (<c>ui</c>, <c>hud</c>, <c>gameover</c>, ...).
    ///  - Never rename a key without bumping every locale asset — locale files store the
    ///    key as a string, not a const reference.
    ///  - When you add a key here, run <b>LoneFighter -> Localization -> Generate English Baseline</b>
    ///    to regenerate <c>Assets/Data/Localization/English.asset</c> with the default English text.
    ///
    /// Adding a key in a new locale: edit the corresponding locale's <c>keys</c>/<c>values</c>
    /// arrays directly; the generator only authoritatively regenerates the English baseline.
    /// </summary>
    public static class LocalizationKeys
    {
        // ----- Top-level UI -----
        public const string UiPlay     = "ui.play";
        public const string UiSettings = "ui.settings";
        public const string UiQuit     = "ui.quit";
        public const string UiBack     = "ui.back";
        public const string UiResume   = "ui.resume";
        public const string UiPause    = "ui.pause";
        public const string UiMenu     = "ui.menu";
        public const string UiClose    = "ui.close";
        public const string UiContinue = "ui.continue";
        public const string UiOk       = "ui.ok";
        public const string UiCancel   = "ui.cancel";

        // ----- HUD -----
        public const string HudKills = "hud.kills";
        public const string HudTimer = "hud.timer";
        public const string HudLevel = "hud.level";
        public const string HudHp    = "hud.hp";
        public const string HudXp    = "hud.xp";

        // ----- Level-up modal -----
        public const string LevelupTitle    = "levelup.title";
        public const string LevelupSubtitle = "levelup.subtitle";
        public const string LevelupSkip     = "levelup.skip";
        public const string LevelupReroll   = "levelup.reroll";

        // ----- Game-over screen -----
        public const string GameoverVictory      = "gameover.victory";
        public const string GameoverDefeat       = "gameover.defeat";
        public const string GameoverRetry        = "gameover.retry";
        public const string GameoverMenu         = "gameover.menu";
        public const string GameoverTimeSurvived = "gameover.time_survived";
        public const string GameoverKills        = "gameover.kills";
        public const string GameoverLevel        = "gameover.level";

        // ----- Settings panel -----
        public const string SettingsTitle      = "settings.title";
        public const string SettingsMaster     = "settings.master_volume";
        public const string SettingsMusic      = "settings.music_volume";
        public const string SettingsSfx        = "settings.sfx_volume";
        public const string SettingsHaptics    = "settings.haptics";
        public const string SettingsTargetFps  = "settings.target_fps";
        public const string SettingsQuality    = "settings.quality";
        public const string SettingsLanguage   = "settings.language";
        public const string SettingsReset      = "settings.reset";
        public const string SettingsFpsUncapped = "settings.fps_uncapped";
        public const string SettingsQualityLow  = "settings.quality_low";
        public const string SettingsQualityMed  = "settings.quality_medium";
        public const string SettingsQualityHigh = "settings.quality_high";

        // ----- Unlock toasts -----
        public const string UnlockFirstBlood = "unlock.first_blood";
        public const string UnlockCenturion  = "unlock.centurion";
        public const string UnlockSurvivor   = "unlock.survivor";
        public const string UnlockMarathoner = "unlock.marathoner";
        public const string UnlockToastTitle = "unlock.toast_title";

        // ----- Tutorial -----
        public const string TutorialMove      = "tutorial.move";
        public const string TutorialDash      = "tutorial.dash";
        public const string TutorialAutofire  = "tutorial.autofire";
        public const string TutorialLevelup   = "tutorial.levelup";
        public const string TutorialContinue  = "tutorial.continue";

        // ----- Weapons -----
        public const string WeaponPistol         = "weapon.pistol";
        public const string WeaponSpreadShotgun  = "weapon.spread_shotgun";
        public const string WeaponChainLightning = "weapon.chain_lightning";
        public const string WeaponOrbitalBlade   = "weapon.orbital_blade";

        // ----- Enemies -----
        public const string EnemyGrunt   = "enemy.grunt";
        public const string EnemyRunner  = "enemy.runner";
        public const string EnemyTank    = "enemy.tank";
        public const string EnemySpitter = "enemy.spitter";
        public const string EnemyDasher  = "enemy.dasher";
        public const string EnemyBomber  = "enemy.bomber";
        public const string EnemyWarden  = "enemy.warden";

        // ----- Pickups -----
        public const string PickupXpGem = "pickup.xp_gem";
        public const string PickupHeal  = "pickup.heal";
        public const string PickupMagnet = "pickup.magnet";
        public const string PickupBomb  = "pickup.bomb";

        // ----- Pause menu -----
        public const string PauseTitle  = "pause.title";

        // ----- Run summary / stats -----
        public const string StatsTitle       = "stats.title";
        public const string StatsBestTime    = "stats.best_time";
        public const string StatsTotalKills  = "stats.total_kills";
        public const string StatsRunsPlayed  = "stats.runs_played";
        public const string StatsVictories   = "stats.victories";
    }
}
