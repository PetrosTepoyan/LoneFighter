using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Player;
using LoneFighter.Weapons;

namespace LoneFighter.UI.WeaponHud
{
    /// <summary>
    /// Top-of-screen horizontal weapon strip. One <see cref="WeaponSlotUi"/> per
    /// active weapon in <see cref="WeaponInventory.Weapons"/>.
    ///
    /// <para>Authoring</para>
    /// Drop this MonoBehaviour onto a UI GameObject anchored top-center under the
    /// existing HP / level bar, with a <c>HorizontalLayoutGroup</c> child that will
    /// receive the slots. Wire <see cref="slotPrefab"/> to a prefab that has a
    /// <see cref="WeaponSlotUi"/> at its root.
    ///
    /// <para>Polling cadence</para>
    /// The inventory's weapon list only changes on level-up (rare), so we re-scan
    /// it via a single 1-second coroutine instead of every frame. When the count
    /// changes — or when individual weapon references change identity — we rebuild
    /// the slot row. The per-frame cooldown / flash work happens *inside each slot*
    /// (see <see cref="WeaponSlotUi"/>); this controller does no Update() work.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponHudController : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Optional explicit inventory. If null, the controller finds " +
                 "PlayerController.Instance and uses GetComponentInChildren<WeaponInventory>().")]
        [SerializeField] private WeaponInventory inventoryOverride;

        [Header("Visuals")]
        [Tooltip("Container that owns the slots. Typically has a HorizontalLayoutGroup.\n" +
                 "If null, slots are parented directly to this transform.")]
        [SerializeField] private RectTransform slotRoot;

        [Tooltip("Prefab with a WeaponSlotUi component at the root.")]
        [SerializeField] private WeaponSlotUi slotPrefab;

        [Tooltip("Icon registry consulted by slots when binding a weapon.")]
        [SerializeField] private WeaponIconRegistry iconRegistry;

        [Header("Polling")]
        [Tooltip("Seconds between inventory checks. The inventory only changes on " +
                 "level-up so 1s is plenty and keeps GC/allocs effectively zero.")]
        [SerializeField] private float pollIntervalSeconds = 1f;

        private readonly List<WeaponSlotUi> _slots = new();
        private readonly List<WeaponBase> _lastBoundWeapons = new();
        private WeaponInventory _inventory;
        private Coroutine _pollLoop;

        private void OnEnable()
        {
            if (slotRoot == null) slotRoot = transform as RectTransform;
            _pollLoop = StartCoroutine(PollLoop());
        }

        private void OnDisable()
        {
            if (_pollLoop != null)
            {
                StopCoroutine(_pollLoop);
                _pollLoop = null;
            }
        }

        private IEnumerator PollLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, pollIntervalSeconds));
            // First sync runs immediately so the HUD appears with the starting weapon
            // on frame ~1 rather than after a full polling interval.
            while (true)
            {
                TryResolveInventory();
                SyncSlotsIfChanged();
                yield return wait;
            }
        }

        private void TryResolveInventory()
        {
            if (_inventory != null) return;

            if (inventoryOverride != null)
            {
                _inventory = inventoryOverride;
                return;
            }

            var player = PlayerController.Instance;
            if (player == null) return;

            _inventory = player.GetComponentInChildren<WeaponInventory>();
        }

        private void SyncSlotsIfChanged()
        {
            if (_inventory == null) return;

            var weapons = _inventory.Weapons;
            if (weapons == null) return;

            // Cheap "did the list change?" check: compare counts then per-index identity.
            // Avoids rebuilding slots every poll when the inventory is unchanged.
            bool changed = weapons.Count != _lastBoundWeapons.Count;
            if (!changed)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    if (!ReferenceEquals(weapons[i], _lastBoundWeapons[i]))
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return;

            RebuildSlots(weapons);
        }

        private void RebuildSlots(IReadOnlyList<WeaponBase> weapons)
        {
            // Ensure pool size matches: instantiate more slots or hide extras.
            while (_slots.Count < weapons.Count)
            {
                var slot = InstantiateSlot();
                if (slot == null) break; // Avoid an infinite loop if the prefab is missing.
                _slots.Add(slot);
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null) continue;
                bool active = i < weapons.Count;
                _slots[i].gameObject.SetActive(active);
                if (active) _slots[i].Bind(weapons[i], iconRegistry);
                else _slots[i].Unbind();
            }

            _lastBoundWeapons.Clear();
            for (int i = 0; i < weapons.Count; i++) _lastBoundWeapons.Add(weapons[i]);
        }

        private WeaponSlotUi InstantiateSlot()
        {
            if (slotPrefab == null)
            {
                Debug.LogWarning("[WeaponHudController] slotPrefab is null; " +
                                 "drag a prefab with a WeaponSlotUi root into the inspector.", this);
                return null;
            }
            var inst = Instantiate(slotPrefab, slotRoot != null ? slotRoot : transform);
            inst.gameObject.name = "WeaponSlotUi";
            return inst;
        }

        /// <summary>
        /// Manual refresh hook (e.g. for tests or for a "Reset HUD" gameplay event).
        /// </summary>
        public void ForceResync()
        {
            _inventory = null;
            TryResolveInventory();
            _lastBoundWeapons.Clear();
            SyncSlotsIfChanged();
        }
    }
}
