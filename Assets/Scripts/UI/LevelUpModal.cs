using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Data;
using LoneFighter.Player;
using LoneFighter.Systems;
using LoneFighter.Weapons;

namespace LoneFighter.UI
{
    public class LevelUpModal : MonoBehaviour
    {
        [System.Serializable]
        public class ChoiceButton
        {
            public Button button;
            public TMP_Text title;
            public TMP_Text description;
            public Image icon;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private List<ChoiceButton> choices = new();
        [SerializeField] private int choiceCount = 3;

        private PlayerLevel _level;
        private WeaponInventory _inventory;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
                _inventory = PlayerController.Instance.GetComponentInChildren<WeaponInventory>();
            }
            if (_level != null) _level.OnLevelUp += HandleLevelUp;
        }

        private void OnDestroy()
        {
            if (_level != null) _level.OnLevelUp -= HandleLevelUp;
        }

        private void HandleLevelUp(int _) => Show();

        public void Show()
        {
            if (UpgradeService.Instance == null) return;
            var picks = UpgradeService.Instance.Roll(Mathf.Min(choiceCount, choices.Count));

            for (int i = 0; i < choices.Count; i++)
            {
                var slot = choices[i];
                if (slot.button == null) continue;

                slot.button.onClick.RemoveAllListeners();
                if (i >= picks.Count)
                {
                    slot.button.gameObject.SetActive(false);
                    continue;
                }

                var upgrade = picks[i];
                slot.button.gameObject.SetActive(true);
                if (slot.title != null) slot.title.text = upgrade.displayName;
                if (slot.description != null) slot.description.text = upgrade.description;
                if (slot.icon != null) slot.icon.sprite = upgrade.icon;
                slot.button.onClick.AddListener(() => Choose(upgrade));
            }

            if (root != null) root.SetActive(true);
        }

        private void Choose(UpgradeData upgrade)
        {
            if (upgrade != null) Apply(upgrade);
            if (UpgradeService.Instance != null) UpgradeService.Instance.RecordChoice(upgrade);
            if (root != null) root.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.ResumeFromLevelUp();
        }

        private void Apply(UpgradeData upgrade)
        {
            if (PlayerController.Instance == null) return;
            var player = PlayerController.Instance;
            var health = player.GetComponent<PlayerHealth>();
            var lvl = player.GetComponent<PlayerLevel>();
            var collector = player.GetComponentInChildren<XPCollector>();

            switch (upgrade.kind)
            {
                case UpgradeKind.WeaponDamage:
                    _inventory?.ApplyDamageBonus(upgrade.magnitude);
                    break;
                case UpgradeKind.WeaponCooldown:
                    _inventory?.ApplyCooldownReduction(upgrade.magnitude);
                    break;
                case UpgradeKind.WeaponProjectileSpeed:
                    _inventory?.ApplyProjectileSpeedBonus(upgrade.magnitude);
                    break;
                case UpgradeKind.WeaponPierce:
                    _inventory?.ApplyPierce(Mathf.RoundToInt(upgrade.magnitude));
                    break;
                case UpgradeKind.PlayerMaxHealth:
                    if (health != null) health.SetMax(health.Max * (1f + upgrade.magnitude), refill: false);
                    break;
                case UpgradeKind.PlayerMoveSpeed:
                    player.MoveSpeed *= 1f + upgrade.magnitude;
                    break;
                case UpgradeKind.PlayerMagnetRadius:
                    if (collector != null) collector.MagnetRadius *= 1f + upgrade.magnitude;
                    break;
                case UpgradeKind.XpGainMultiplier:
                    if (lvl != null) lvl.XpMultiplier *= 1f + upgrade.magnitude;
                    break;
                case UpgradeKind.GrantWeapon:
                    if (upgrade.weaponToGrant != null) _inventory?.GrantWeapon(upgrade.weaponToGrant);
                    break;
            }
        }
    }
}
