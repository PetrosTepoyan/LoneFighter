using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Systems;

namespace LoneFighter.UI
{
    public class PauseController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button menuButton;

        private void Awake()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void Start()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(Toggle);
            if (resumeButton != null) resumeButton.onClick.AddListener(Toggle);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleState;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleState;
        }

        private void HandleState(GameState prev, GameState next)
        {
            if (pausePanel != null) pausePanel.SetActive(next == GameState.Paused);
        }

        private void Toggle()
        {
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
        }

        private void GoToMenu()
        {
            if (GameManager.Instance != null) GameManager.Instance.LoadMainMenu();
        }
    }
}
