using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Attach to any damageable (enemy or player). Manages a list of active <see cref="StatusEffect"/>s,
    /// ticks them on FixedUpdate, and notifies listeners (icons / VFX hooks) when effects are added/removed.
    ///
    /// Public API:
    ///   - Apply(StatusEffect)        : add or stack an effect
    ///   - Has(string id)             : true if any active effect matches id
    ///   - RemoveAll()                : clear everything (e.g. on death / respawn)
    ///   - ActiveEffects (read-only)  : enumerate current effects (for UI)
    ///   - OnEffectAdded / OnEffectRemoved : observer hooks
    ///
    /// This component is "victim-agnostic" — it stores a cached <see cref="Rigidbody2D"/> handle that
    /// effects may use (Stun, Slow), but never directly references EnemyHealth / PlayerHealth.
    /// Effects use TryGetComponent so the same controller works on both player and enemies.
    /// </summary>
    [DisallowMultipleComponent]
    public class StatusController : MonoBehaviour
    {
        private readonly List<StatusEffect> _active = new();
        // Scratch buffer reused on each FixedUpdate to avoid GC while removing expired effects.
        private readonly List<StatusEffect> _toRemove = new();

        /// <summary>Rigidbody2D on this GameObject, if any. Cached lazily by effects.</summary>
        public Rigidbody2D Body { get; private set; }

        /// <summary>Read-only view of currently active effects.</summary>
        public IReadOnlyList<StatusEffect> ActiveEffects => _active;

        /// <summary>Fired after an effect has been applied (either fresh or via stack/refresh).</summary>
        public event Action<StatusController, StatusEffect> OnEffectAdded;

        /// <summary>Fired after an effect has been removed (expiry or RemoveAll).</summary>
        public event Action<StatusController, StatusEffect> OnEffectRemoved;

        private void Awake()
        {
            TryGetComponent(out Rigidbody2D rb);
            Body = rb;
        }

        private void OnDisable()
        {
            // The owner GameObject went inactive (pooled enemy returned, scene unload, etc.).
            // Clear cleanly so a re-used pooled enemy doesn't carry stale effects.
            RemoveAll();
        }

        /// <summary>
        /// Apply an effect to this victim. If an effect with the same Id already exists,
        /// the existing one's <see cref="StatusEffect.OnReapply"/> decides the merge behavior
        /// and the incoming instance is discarded.
        /// </summary>
        public void Apply(StatusEffect effect)
        {
            if (effect == null) return;

            // Stacking dispatch: existing effect of the same Id handles the merge.
            for (int i = 0; i < _active.Count; i++)
            {
                var existing = _active[i];
                if (existing.Id == effect.Id)
                {
                    existing.OnReapply(this, effect);
                    // Notify listeners so icon UI / VFX can react to the refresh as if "added again".
                    OnEffectAdded?.Invoke(this, existing);
                    return;
                }
            }

            float now = Time.time;
            effect.StartTime = now;
            effect.ExpiresAt = now + effect.duration;
            // First tick happens after one interval (so OnApply does the "instant" work).
            effect.NextTickTime = now + Mathf.Max(0f, effect.tickInterval);
            _active.Add(effect);

            try { effect.OnApply(this); }
            catch (Exception e) { Debug.LogException(e, this); }

            OnEffectAdded?.Invoke(this, effect);
        }

        /// <summary>True if any active effect has the given Id.</summary>
        public bool Has(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id == id) return true;
            }
            return false;
        }

        /// <summary>Remove all active effects, firing OnRemove + OnEffectRemoved for each.</summary>
        public void RemoveAll()
        {
            if (_active.Count == 0) return;
            // Iterate from end so concurrent-modification is impossible even if OnRemove throws.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var fx = _active[i];
                _active.RemoveAt(i);
                try { fx.OnRemove(this); }
                catch (Exception e) { Debug.LogException(e, this); }
                OnEffectRemoved?.Invoke(this, fx);
            }
        }

        private void FixedUpdate()
        {
            if (_active.Count == 0) return;
            float now = Time.time;

            // Tick pass. We allow a tick on the same frame the effect expires, so a DoT
            // that runs e.g. 3s @ 0.5s yields the full 6 ticks (start, 0.5, 1.0, ... 3.0).
            for (int i = 0; i < _active.Count; i++)
            {
                var fx = _active[i];
                if (now >= fx.NextTickTime)
                {
                    try { fx.OnTick(this); }
                    catch (Exception e) { Debug.LogException(e, this); }
                    // Schedule next tick. tickInterval==0 means "tick every FixedUpdate".
                    float step = fx.tickInterval > 0f ? fx.tickInterval : Time.fixedDeltaTime;
                    fx.NextTickTime += step;
                }

                if (now >= fx.ExpiresAt)
                {
                    _toRemove.Add(fx);
                }
            }

            // Removal pass (deferred so we don't shift indices mid-tick).
            if (_toRemove.Count > 0)
            {
                for (int i = 0; i < _toRemove.Count; i++)
                {
                    var fx = _toRemove[i];
                    _active.Remove(fx);
                    try { fx.OnRemove(this); }
                    catch (Exception e) { Debug.LogException(e, this); }
                    OnEffectRemoved?.Invoke(this, fx);
                }
                _toRemove.Clear();
            }
        }

        /// <summary>
        /// Convenience for effects that need to find an existing instance of their own type.
        /// Returns null if not present. Intended for effects that stack-count themselves.
        /// </summary>
        public T Find<T>() where T : StatusEffect
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] is T t) return t;
            }
            return null;
        }
    }
}
