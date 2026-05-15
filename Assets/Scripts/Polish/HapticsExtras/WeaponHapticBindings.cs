using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;
using LoneFighter.UI.Settings;
using LoneFighter.Weapons;

namespace LoneFighter.Polish.HapticsExtras
{
    /// <summary>
    /// Per-weapon hit feedback. Detects when each <see cref="WeaponBase"/> in
    /// <see cref="WeaponInventory.Weapons"/> fires and plays a short Light haptic pulse so the
    /// player can feel their gun cadence in the controller / phone.
    ///
    /// <para><b>Detection strategy</b></para>
    /// <list type="number">
    ///   <item><description>If a C# event named <c>OnFired</c> / <c>OnShoot</c> /
    ///       <c>OnAttack</c> exists on the weapon (via reflection), subscribe to it directly.</description></item>
    ///   <item><description>Otherwise we poll the protected <c>_cooldownTimer</c> field. A reset
    ///       from "small positive" back to "≥ CurrentCooldown - ε" indicates a successful fire
    ///       (see <see cref="WeaponBase.Update"/>). We only need this once per weapon, then we
    ///       sample on each LateUpdate.</description></item>
    /// </list>
    /// The reflection probe runs lazily and caches results per weapon type so the steady-state
    /// cost is a single field read + float compare per active weapon per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponHapticBindings : MonoBehaviour
    {
        [Header("Inventory")]
        [Tooltip("Inventory to bind to. If null, we search for one on PlayerController in Start.")]
        [SerializeField] private WeaponInventory inventory;

        [Header("Cadence Limiter")]
        [Tooltip("Minimum seconds between two weapon-fire haptics. Prevents fast weapons from " +
                 "buzzing every frame.")]
        [SerializeField, Min(0f)] private float minPulseIntervalSeconds = 0.06f;

        [Tooltip("How often (seconds) we re-scan the inventory for newly granted weapons.")]
        [SerializeField, Min(0.1f)] private float inventoryScanInterval = 1f;

        [Header("Intensity")]
        [Tooltip("Pulse intensity for a single weapon shot.")]
        [SerializeField] private HapticIntensity weaponShotIntensity = HapticIntensity.Light;

        // ---- Internal -------------------------------------------------------------------

        // One entry per tracked WeaponBase. Cleaned up if the weapon dies / is destroyed.
        private readonly List<WeaponTrack> _tracks = new();
        private readonly Dictionary<System.Type, FieldInfo> _cooldownFieldCache = new();
        private float _nextInventoryScan;
        private float _lastPulseTime = -10f;

        private class WeaponTrack
        {
            public WeaponBase Weapon;
            public FieldInfo CooldownField;
            public float LastCooldownReading;
            public bool BoundViaEvent;
            public System.Delegate BoundDelegate;
            public EventInfo BoundEvent;
        }

        // ---- Lifecycle --------------------------------------------------------------------

        private void Start()
        {
            ResolveInventory();
            ScanInventory();
        }

        private void OnDisable()
        {
            // Detach any event subscriptions cleanly.
            for (int i = 0; i < _tracks.Count; i++)
            {
                UnbindEvent(_tracks[i]);
            }
            _tracks.Clear();
            _nextInventoryScan = 0f;
        }

        private void OnEnable()
        {
            // Don't double-scan on the first frame — Start() handles initial pickup.
            _nextInventoryScan = Time.unscaledTime + inventoryScanInterval;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextInventoryScan)
            {
                _nextInventoryScan = Time.unscaledTime + inventoryScanInterval;
                ScanInventory();
            }

            // Poll cooldown timers for fire detection.
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var track = _tracks[i];
                if (track.Weapon == null)
                {
                    UnbindEvent(track);
                    _tracks.RemoveAt(i);
                    continue;
                }
                if (track.BoundViaEvent) continue; // Event-driven; nothing to poll.

                if (track.CooldownField == null) continue;

                float now = (float)track.CooldownField.GetValue(track.Weapon);
                // A successful fire bumps the timer back up to CurrentCooldown (typically 0.3-1.5s).
                // A no-target "miss" bumps it to 0.1f — we reject that with the 0.15 floor.
                if (now - track.LastCooldownReading > 0.15f)
                {
                    OnWeaponFired(track.Weapon);
                }
                track.LastCooldownReading = now;
            }
        }

        // ---- Inventory scanning ---------------------------------------------------------

        private void ResolveInventory()
        {
            if (inventory != null) return;
            if (PlayerController.Instance != null)
            {
                inventory = PlayerController.Instance.GetComponentInChildren<WeaponInventory>();
            }
            if (inventory == null)
            {
#if UNITY_2023_1_OR_NEWER
                inventory = UnityEngine.Object.FindFirstObjectByType<WeaponInventory>();
#else
                inventory = UnityEngine.Object.FindObjectOfType<WeaponInventory>();
#endif
            }
        }

        private void ScanInventory()
        {
            ResolveInventory();
            if (inventory == null) return;

            var weapons = inventory.Weapons;
            if (weapons == null) return;

            // Discover new weapons.
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                if (w == null) continue;
                if (FindTrack(w) != null) continue;

                var track = new WeaponTrack { Weapon = w };
                if (!TryBindEvent(track))
                {
                    track.CooldownField = ResolveCooldownField(w.GetType());
                    if (track.CooldownField != null)
                    {
                        track.LastCooldownReading = (float)track.CooldownField.GetValue(w);
                    }
                }
                _tracks.Add(track);
            }

            // Drop tracks for weapons that no longer exist in the inventory.
            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var t = _tracks[i];
                if (t.Weapon == null || !InventoryContains(weapons, t.Weapon))
                {
                    UnbindEvent(t);
                    _tracks.RemoveAt(i);
                }
            }
        }

        private static bool InventoryContains(IReadOnlyList<WeaponBase> list, WeaponBase target)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], target)) return true;
            }
            return false;
        }

        private WeaponTrack FindTrack(WeaponBase w)
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i].Weapon == w) return _tracks[i];
            }
            return null;
        }

        // ---- Event reflection -----------------------------------------------------------

        private static readonly string[] CandidateEventNames = { "OnFired", "OnShoot", "OnAttack", "OnFire" };

        private bool TryBindEvent(WeaponTrack track)
        {
            var type = track.Weapon.GetType();
            for (int i = 0; i < CandidateEventNames.Length; i++)
            {
                var evt = type.GetEvent(CandidateEventNames[i],
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (evt == null) continue;

                // We can only bind if the event handler type matches Action or Action<WeaponBase>.
                var handlerType = evt.EventHandlerType;
                System.Delegate handler = null;
                var weapon = track.Weapon;

                if (handlerType == typeof(System.Action))
                {
                    System.Action cb = () => OnWeaponFired(weapon);
                    handler = cb;
                }
                else if (handlerType == typeof(System.Action<WeaponBase>))
                {
                    System.Action<WeaponBase> cb = w => OnWeaponFired(w);
                    handler = cb;
                }

                if (handler != null)
                {
                    try
                    {
                        evt.AddEventHandler(weapon, handler);
                        track.BoundViaEvent = true;
                        track.BoundDelegate = handler;
                        track.BoundEvent = evt;
                        return true;
                    }
                    catch
                    {
                        // Reflection add failed — fall through to polling.
                    }
                }
            }
            return false;
        }

        private void UnbindEvent(WeaponTrack track)
        {
            if (track == null || !track.BoundViaEvent) return;
            try
            {
                if (track.Weapon != null && track.BoundEvent != null && track.BoundDelegate != null)
                {
                    track.BoundEvent.RemoveEventHandler(track.Weapon, track.BoundDelegate);
                }
            }
            catch
            {
                // Detach is best-effort.
            }
            track.BoundViaEvent = false;
            track.BoundDelegate = null;
            track.BoundEvent = null;
        }

        // ---- Cooldown field reflection (cached per Type) --------------------------------

        private FieldInfo ResolveCooldownField(System.Type type)
        {
            if (type == null) return null;
            if (_cooldownFieldCache.TryGetValue(type, out var cached)) return cached;

            FieldInfo found = null;
            var t = type;
            while (t != null && found == null)
            {
                found = t.GetField("_cooldownTimer",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                t = t.BaseType;
            }
            // Validate type — we expect a float.
            if (found != null && found.FieldType != typeof(float)) found = null;

            _cooldownFieldCache[type] = found;
            return found;
        }

        // ---- Fire dispatch -------------------------------------------------------------

        private void OnWeaponFired(WeaponBase weapon)
        {
            if (weapon == null) return;
            if (!GameSettings.HapticsEnabled) return;
            if (HapticsSettings.IsEffectivelyMuted) return;

            float now = Time.unscaledTime;
            if (now - _lastPulseTime < minPulseIntervalSeconds) return;
            _lastPulseTime = now;

            var svc = HapticsService.Instance;
            if (svc == null) return;

            // Weapon-type-specific override hook: if a weapon implements a method
            // `HapticIntensity GetHapticIntensity()` we honor it (reflection-based, optional).
            HapticIntensity intensity = ResolvePerWeaponIntensity(weapon);
            svc.Pulse(intensity);
        }

        private HapticIntensity ResolvePerWeaponIntensity(WeaponBase weapon)
        {
            try
            {
                var m = weapon.GetType().GetMethod("GetHapticIntensity",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    null, System.Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(HapticIntensity))
                {
                    return (HapticIntensity)m.Invoke(weapon, null);
                }
            }
            catch
            {
                // Optional override — ignore failures.
            }
            return weaponShotIntensity;
        }
    }
}
