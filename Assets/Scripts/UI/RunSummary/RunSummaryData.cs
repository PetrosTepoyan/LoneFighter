using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.UI.RunSummary
{
    /// <summary>
    /// Plain-data container describing a completed run. Built once when the run-summary panel
    /// is shown and then read-only for display purposes.
    ///
    /// Fields the scaffold can populate today are filled in by <see cref="Build"/>; everything
    /// else defaults to empty/zero so future wiring (e.g. kills-by-enemy tracking, upgrade-pick
    /// history) can fill in the blanks without breaking this consumer.
    /// </summary>
    public struct RunSummaryData
    {
        public bool victory;
        public float survivedSeconds;
        public int kills;
        public int peakLevel;
        public int xpEarned;
        public string mvpWeapon;

        /// <summary>Kills bucketed by enemy display name (or kind). Iterate with <c>foreach</c>.</summary>
        public Dictionary<string, int> killsByEnemy;

        /// <summary>Display names of upgrades the player picked during the run, in order.</summary>
        public List<string> upgradesPicked;

        /// <summary>
        /// Best-effort snapshot of the current run from already-existing singletons.
        /// Anything we don't have a source for is left empty/zero — extending the wiring
        /// (e.g. RecordKillByKind hooks in EnemyHealth, RecordUpgradeChoice hooks in
        /// UpgradeService) is a future task.
        /// </summary>
        public static RunSummaryData Build()
        {
            var data = new RunSummaryData
            {
                victory = false,
                survivedSeconds = 0f,
                kills = 0,
                peakLevel = 1,
                xpEarned = 0,
                mvpWeapon = string.Empty,
                killsByEnemy = new Dictionary<string, int>(),
                upgradesPicked = new List<string>(),
            };

            // Pull what we can from GameManager.
            var gm = GameManager.Instance;
            if (gm != null)
            {
                data.survivedSeconds = gm.ElapsedSeconds;
                data.kills = gm.Kills;
                // GameState.Victory after TriggerVictory; GameOver after TriggerGameOver.
                data.victory = gm.State == GameState.Victory;
            }

            // Pull level + accumulated XP from the player, if still alive in the scene graph.
            // GameOver loads a separate scene so PlayerController.Instance is usually null
            // by the time this builds; in that case we fall back to whatever the GameManager
            // exposes (currently nothing — so 0/empty is correct until wiring is added).
            var player = PlayerController.Instance;
            if (player != null)
            {
                var level = player.GetComponent<PlayerLevel>();
                if (level != null)
                {
                    data.peakLevel = level.Level;
                    // XP-earned-this-run isn't tracked separately yet; the current bucket
                    // (CurrentXp) is the best available proxy.
                    data.xpEarned = level.CurrentXp;
                }
            }

            return data;
        }
    }
}
