using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoneFighter.Systems
{
    public enum GameState { Boot, Playing, LevelUp, Paused, GameOver, Victory }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public float ElapsedSeconds { get; private set; }
        public int Kills { get; private set; }

        public event Action<GameState, GameState> OnStateChanged;
        public event Action<int> OnKillsChanged;

        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameScene = "Game";
        [SerializeField] private string gameOverScene = "GameOver";

        public string LastRunSummary { get; private set; } = string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Push high-refresh-rate displays as hard as the device allows.
            // On iPhone ProMotion / Android 120Hz panels this hits 120; on 60Hz it clamps to 60.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BeginRun()
        {
            ElapsedSeconds = 0f;
            Kills = 0;
            OnKillsChanged?.Invoke(Kills);
            SetState(GameState.Playing);
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                ElapsedSeconds += Time.deltaTime;
            }
        }

        public void RegisterKill()
        {
            Kills++;
            OnKillsChanged?.Invoke(Kills);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void EnterLevelUp() => SetState(GameState.LevelUp);
        public void ResumeFromLevelUp() => SetState(GameState.Playing);

        public void TriggerGameOver()
        {
            LastRunSummary = $"Kills: {Kills}  •  Time: {FormatTime(ElapsedSeconds)}";
            SetState(GameState.GameOver);
            SceneManager.LoadScene(gameOverScene);
        }

        public void TriggerVictory()
        {
            LastRunSummary = $"VICTORY!  Kills: {Kills}  •  Time: {FormatTime(ElapsedSeconds)}";
            SetState(GameState.Victory);
            SceneManager.LoadScene(gameOverScene);
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SetState(GameState.Boot);
            SceneManager.LoadScene(mainMenuScene);
        }

        public void LoadGameScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameScene);
        }

        private void SetState(GameState next)
        {
            if (next == State) return;
            var prev = State;
            State = next;

            Time.timeScale = next switch
            {
                GameState.Playing => 1f,
                GameState.Boot => 1f,
                GameState.LevelUp => 0f,
                GameState.Paused => 0f,
                GameState.GameOver => 1f,
                GameState.Victory => 1f,
                _ => 1f
            };

            OnStateChanged?.Invoke(prev, next);
        }

        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }
    }
}
