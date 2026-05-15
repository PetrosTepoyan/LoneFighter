using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.UI
{
    public class HudController : MonoBehaviour
    {
        [Header("Bars")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider xpBar;

        [Header("Text")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text killsText;

        private PlayerHealth _health;
        private PlayerLevel _level;

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(_health.Current, _health.Max);
            }
            if (_level != null)
            {
                _level.OnXpChanged += HandleXpChanged;
                _level.OnLevelUp += HandleLevelUp;
                HandleXpChanged(_level.CurrentXp, _level.XpToNext);
                HandleLevelUp(_level.Level);
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnKillsChanged += HandleKillsChanged;
                HandleKillsChanged(GameManager.Instance.Kills);
            }
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            if (_level != null)
            {
                _level.OnXpChanged -= HandleXpChanged;
                _level.OnLevelUp -= HandleLevelUp;
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnKillsChanged -= HandleKillsChanged;
            }
        }

        private void Update()
        {
            if (timerText != null && GameManager.Instance != null)
            {
                timerText.text = GameManager.FormatTime(GameManager.Instance.ElapsedSeconds);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (healthBar == null) return;
            healthBar.maxValue = max;
            healthBar.value = current;
        }

        private void HandleXpChanged(int current, int toNext)
        {
            if (xpBar == null) return;
            xpBar.maxValue = toNext;
            xpBar.value = current;
        }

        private void HandleLevelUp(int level)
        {
            if (levelText != null) levelText.text = $"Lv {level}";
        }

        private void HandleKillsChanged(int kills)
        {
            if (killsText != null) killsText.text = kills.ToString();
        }
    }
}
