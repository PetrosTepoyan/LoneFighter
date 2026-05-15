using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Systems;

namespace LoneFighter.UI
{
    public class GameOverController : MonoBehaviour
    {
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        private void Start()
        {
            if (summaryText != null && GameManager.Instance != null)
            {
                summaryText.text = GameManager.Instance.LastRunSummary;
            }
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenu);
        }

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
