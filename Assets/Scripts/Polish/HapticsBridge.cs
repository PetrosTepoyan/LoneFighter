using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Polish
{
    /// Auto-fires HapticsService.Pulse on key game events. Toggleable for testing.
    public class HapticsBridge : MonoBehaviour
    {
        [Header("Enable Channels")]
        [SerializeField] private bool enabledForDamage = true;
        [SerializeField] private bool enabledForKills = true;
        [SerializeField] private bool enabledForLevelUp = true;

        [Header("Kill Rate Limiting")]
        [Tooltip("Only buzz every N kills to avoid constant low-rumble.")]
        [SerializeField, Min(1)] private int killBuzzEveryN = 5;

        private PlayerHealth _health;
        private PlayerLevel _level;
        private float _lastKnownHealth = -1f;
        private int _lastKillCount;

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
            if (_health != null)
            {
                _lastKnownHealth = _health.Current;
                _health.OnHealthChanged += HandleHealthChanged;
            }
            if (_level != null)
            {
                _level.OnLevelUp += HandleLevelUp;
            }
            if (GameManager.Instance != null)
            {
                _lastKillCount = GameManager.Instance.Kills;
                GameManager.Instance.OnKillsChanged += HandleKillsChanged;
            }
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            if (_level != null) _level.OnLevelUp -= HandleLevelUp;
            if (GameManager.Instance != null) GameManager.Instance.OnKillsChanged -= HandleKillsChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastKnownHealth < 0f)
            {
                _lastKnownHealth = current;
                return;
            }
            if (enabledForDamage && current < _lastKnownHealth)
            {
                if (HapticsService.Instance != null) HapticsService.Instance.Pulse(HapticIntensity.Heavy);
            }
            _lastKnownHealth = current;
        }

        private void HandleLevelUp(int level)
        {
            if (!enabledForLevelUp) return;
            if (HapticsService.Instance != null) HapticsService.Instance.Pulse(HapticIntensity.Medium);
        }

        private void HandleKillsChanged(int totalKills)
        {
            if (!enabledForKills)
            {
                _lastKillCount = totalKills;
                return;
            }
            if (totalKills < _lastKillCount)
            {
                // Run reset.
                _lastKillCount = totalKills;
                return;
            }
            // Buzz when we cross a multiple-of-N boundary.
            int prevBucket = _lastKillCount / killBuzzEveryN;
            int nextBucket = totalKills / killBuzzEveryN;
            _lastKillCount = totalKills;
            if (nextBucket > prevBucket && HapticsService.Instance != null)
            {
                HapticsService.Instance.Pulse(HapticIntensity.Light);
            }
        }
    }
}
