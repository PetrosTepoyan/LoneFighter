using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Systems;

namespace LoneFighter.UI.RunSummary
{
    /// <summary>
    /// Rich end-of-run summary panel. On enable, snapshots a <see cref="RunSummaryData"/>
    /// from the live systems, fills in static text fields, animates numeric values
    /// counting up, and rebuilds the kills-by-enemy + upgrades-picked lists.
    ///
    /// Designed to coexist with the existing <c>GameOverController</c> in the GameOver scene:
    /// you can either replace its content area with one of these panels, or run both in
    /// parallel (this one populates richer information; the legacy controller can stay for
    /// summary text fallback).
    /// </summary>
    public class RunSummaryPanel : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("Shown when the player won (currently Victory state).")]
        [SerializeField] private string victoryLabel = "VICTORY!";
        [Tooltip("Shown when the player died.")]
        [SerializeField] private string defeatLabel = "DEFEATED";

        [Header("Stat Fields")]
        [SerializeField] private TMP_Text survivedText;
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text peakLevelText;
        [SerializeField] private TMP_Text xpEarnedText;
        [SerializeField] private TMP_Text mvpWeaponText;

        [Header("Kills By Enemy")]
        [Tooltip("Parent transform that receives one row per enemy kind.")]
        [SerializeField] private Transform killsByEnemyRoot;
        [Tooltip("Prefab with a TMP_Text component; one instantiated per (enemy, count) pair.")]
        [SerializeField] private GameObject killsByEnemyRowPrefab;

        [Header("Upgrades Picked")]
        [Tooltip("Parent transform that receives one row per chosen upgrade.")]
        [SerializeField] private Transform upgradesRoot;
        [Tooltip("Prefab with a TMP_Text component; one instantiated per upgrade pick.")]
        [SerializeField] private GameObject upgradeRowPrefab;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        [Header("Animation")]
        [Tooltip("Seconds taken to count numeric values up to their final value.")]
        [SerializeField] private float countUpSeconds = 0.6f;

        // Track instantiated rows so re-populating doesn't leak children if the panel is
        // enabled more than once in the same scene lifetime.
        private readonly List<GameObject> _killRows = new();
        private readonly List<GameObject> _upgradeRows = new();

        private Coroutine _survivedRoutine;

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (menuButton != null)  menuButton.onClick.AddListener(OnMenu);
        }

        private void OnDestroy()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(OnRetry);
            if (menuButton != null)  menuButton.onClick.RemoveListener(OnMenu);
        }

        private void OnEnable()
        {
            var data = RunSummaryData.Build();
            Populate(data);
        }

        private void OnDisable()
        {
            if (_survivedRoutine != null)
            {
                StopCoroutine(_survivedRoutine);
                _survivedRoutine = null;
            }
        }

        /// <summary>
        /// Render the supplied <paramref name="data"/> into the panel. Exposed publicly so
        /// callers can build a custom snapshot (e.g. tests) instead of using <see cref="RunSummaryData.Build"/>.
        /// </summary>
        public void Populate(RunSummaryData data)
        {
            // Title
            if (titleText != null) titleText.text = data.victory ? victoryLabel : defeatLabel;

            // MVP weapon — fall back to "—" when not yet tracked so the field doesn't look broken.
            if (mvpWeaponText != null)
            {
                mvpWeaponText.text = string.IsNullOrWhiteSpace(data.mvpWeapon) ? "—" : data.mvpWeapon;
            }

            // Numeric fields — animate via CountUpText where available, otherwise fall back
            // to setting the text directly so the panel still works without count-up components.
            AnimateInt(killsText, data.kills);
            AnimateInt(peakLevelText, data.peakLevel);
            AnimateInt(xpEarnedText, data.xpEarned);

            // Survived time is a float, so it needs its own coroutine — CountUpText only
            // handles ints.
            StartSurvivedAnimation(data.survivedSeconds);

            // Lists
            RebuildKillsByEnemy(data.killsByEnemy);
            RebuildUpgrades(data.upgradesPicked);
        }

        // -----------------------------------------------------------------------------------
        // Numeric animations
        // -----------------------------------------------------------------------------------

        private void AnimateInt(TMP_Text label, int value)
        {
            if (label == null) return;
            var countUp = label.GetComponent<CountUpText>();
            if (countUp != null)
            {
                countUp.SetTarget(value, countUpSeconds);
            }
            else
            {
                label.text = value.ToString();
            }
        }

        private void StartSurvivedAnimation(float seconds)
        {
            if (survivedText == null) return;

            if (_survivedRoutine != null)
            {
                StopCoroutine(_survivedRoutine);
                _survivedRoutine = null;
            }

            if (countUpSeconds <= 0f || !isActiveAndEnabled)
            {
                survivedText.text = GameManager.FormatTime(seconds);
                return;
            }

            _survivedRoutine = StartCoroutine(CountUpSeconds(seconds, countUpSeconds));
        }

        private IEnumerator CountUpSeconds(float target, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var u = Mathf.Clamp01(t / duration);
                // Same cubic ease-out as CountUpText so the panel feels cohesive.
                var eased = 1f - Mathf.Pow(1f - u, 3f);
                var current = Mathf.LerpUnclamped(0f, target, eased);
                if (survivedText != null) survivedText.text = GameManager.FormatTime(current);
                yield return null;
            }
            if (survivedText != null) survivedText.text = GameManager.FormatTime(target);
            _survivedRoutine = null;
        }

        // -----------------------------------------------------------------------------------
        // Row builders
        // -----------------------------------------------------------------------------------

        private void RebuildKillsByEnemy(Dictionary<string, int> killsByEnemy)
        {
            ClearRows(_killRows);
            if (killsByEnemyRoot == null || killsByEnemyRowPrefab == null) return;
            if (killsByEnemy == null) return;

            foreach (var pair in killsByEnemy)
            {
                var row = Instantiate(killsByEnemyRowPrefab, killsByEnemyRoot);
                row.SetActive(true);
                var text = row.GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (text != null) text.text = $"{pair.Key}: {pair.Value}";
                _killRows.Add(row);
            }
        }

        private void RebuildUpgrades(List<string> upgradesPicked)
        {
            ClearRows(_upgradeRows);
            if (upgradesRoot == null || upgradeRowPrefab == null) return;
            if (upgradesPicked == null) return;

            foreach (var upgrade in upgradesPicked)
            {
                if (string.IsNullOrEmpty(upgrade)) continue;
                var row = Instantiate(upgradeRowPrefab, upgradesRoot);
                row.SetActive(true);
                var text = row.GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (text != null) text.text = upgrade;
                _upgradeRows.Add(row);
            }
        }

        private static void ClearRows(List<GameObject> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null) Destroy(rows[i]);
            }
            rows.Clear();
        }

        // -----------------------------------------------------------------------------------
        // Button handlers
        // -----------------------------------------------------------------------------------

        private void OnRetry()
        {
            if (GameManager.Instance != null) GameManager.Instance.LoadGameScene();
        }

        private void OnMenu()
        {
            if (GameManager.Instance != null) GameManager.Instance.LoadMainMenu();
        }
    }
}
