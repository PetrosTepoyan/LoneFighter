using TMPro;
using UnityEngine;

namespace LoneFighter.Meta
{
    // Read-only display of all-time stats. Drop this onto a Main Menu panel and wire the TMP
    // refs in the inspector. Refreshes whenever the panel is enabled, which covers the typical
    // "return from Game Over -> Main Menu" flow.
    public class StatsPanel : MonoBehaviour
    {
        [Header("TMP Text references")]
        [SerializeField] private TMP_Text totalRunsText;
        [SerializeField] private TMP_Text totalVictoriesText;
        [SerializeField] private TMP_Text totalKillsText;
        [SerializeField] private TMP_Text longestSurvivalText;
        [SerializeField] private TMP_Text totalPlayTimeText;
        [SerializeField] private TMP_Text bestKillStreakText;

        [Header("Optional formatting")]
        [SerializeField] private string totalRunsFormat = "Runs: {0}";
        [SerializeField] private string totalVictoriesFormat = "Victories: {0}";
        [SerializeField] private string totalKillsFormat = "Kills: {0}";
        [SerializeField] private string longestSurvivalFormat = "Longest: {0}";
        [SerializeField] private string totalPlayTimeFormat = "Played: {0}";
        [SerializeField] private string bestKillStreakFormat = "Best Streak: {0}";

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var data = SaveService.Current;
            var totals = data != null ? data.totals : null;
            if (totals == null) totals = new AllTimeStats();

            SetText(totalRunsText, totalRunsFormat, totals.totalRuns);
            SetText(totalVictoriesText, totalVictoriesFormat, totals.totalVictories);
            SetText(totalKillsText, totalKillsFormat, totals.totalKills);
            SetText(longestSurvivalText, longestSurvivalFormat, FormatMinSec(totals.longestSurvival));
            SetText(totalPlayTimeText, totalPlayTimeFormat, FormatHoursMinutes(totals.totalPlayTimeSec));
            SetText(bestKillStreakText, bestKillStreakFormat, totals.bestKillStreak);
        }

        private static void SetText(TMP_Text field, string fmt, object value)
        {
            if (field == null) return;
            field.text = string.IsNullOrEmpty(fmt) ? value?.ToString() : string.Format(fmt, value);
        }

        // hh:mm — for total play time, which can grow into the dozens of hours.
        public static string FormatHoursMinutes(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            return $"{hours:00}:{minutes:00}";
        }

        // mm:ss — for a single run's survival time, which is bounded by the 5-minute run cap.
        public static string FormatMinSec(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return $"{m:00}:{s:00}";
        }
    }
}
