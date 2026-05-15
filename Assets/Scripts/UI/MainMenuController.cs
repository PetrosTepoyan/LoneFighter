using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Systems;

namespace LoneFighter.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        private void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        }

        private void OnPlay()
        {
            if (GameManager.Instance != null) GameManager.Instance.LoadGameScene();
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
