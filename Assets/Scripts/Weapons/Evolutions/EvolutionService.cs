// LoneFighter - Weapon Evolution System
// New file. Do not modify existing files.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LoneFighter.Weapons.Evolutions
{
    /// <summary>
    /// Runtime singleton that owns the Vampire-Survivors-style evolution check.
    ///
    /// Lifecycle:
    ///   - Bootstraps on first scene load via <see cref="Bootstrap"/>.
    ///   - Subscribes to <c>PlayerLevel.OnLevelUp</c> (event or UnityEvent, located via
    ///     reflection so this assembly need not reference Player code).
    ///   - On every level-up, evaluates each <see cref="WeaponEvolutionRecipe"/> in the
    ///     <see cref="EvolutionRecipeRegistry"/>. When a recipe is satisfied AND was not
    ///     satisfied the previous level-up, raises <see cref="OnEvolutionAvailable"/>.
    ///   - Also raises <see cref="OnEvolutionReady"/> the FIRST tick a recipe transitions
    ///     from "not satisfied" to "satisfied" - used by EvolutionToast to show the
    ///     READY-TO-EVOLVE HUD badge before the player actually levels up.
    ///
    /// The service never auto-applies an evolution; the UI (EvolutionModal) decides.
    /// Apply happens via <see cref="ApplyEvolution(WeaponEvolutionRecipe)"/>.
    ///
    /// Reflection contract (best-effort - all checks are wrapped in try/catch and degrade
    /// gracefully if a method/property is missing):
    ///   - PlayerLevel: static event <c>OnLevelUp</c> (Action or Action&lt;int&gt;) OR
    ///                  instance UnityEvent of the same name on a MonoBehaviour singleton.
    ///   - WeaponInventory: instance method <c>HasWeapon(WeaponData)</c>,
    ///                      <c>GetWeaponRank(WeaponData)</c> returning int,
    ///                      <c>GetMaxRank(WeaponData)</c> returning int,
    ///                      <c>RemoveWeapon(WeaponData)</c> bool,
    ///                      <c>GrantWeapon(WeaponData)</c>.
    ///   - UpgradeService: instance method <c>GetStackCount(string upgradeName)</c> returning int,
    ///                     OR <c>HasUpgrade(string)</c>.
    ///
    /// If any of these signatures are missing the service logs a one-time warning and the
    /// matching recipe is treated as un-satisfiable.
    /// </summary>
    public sealed class EvolutionService
    {
        private static EvolutionService _instance;
        public static EvolutionService Instance => _instance ?? (_instance = new EvolutionService());

        /// <summary>Fired on level-up when a recipe is fully satisfied. UI shows the modal.</summary>
        public event Action<WeaponEvolutionRecipe> OnEvolutionAvailable;

        /// <summary>
        /// Fired the first time a recipe transitions to "ready" (base weapon maxed +
        /// passive owned). Used by EvolutionToast - independent of level-ups.
        /// </summary>
        public event Action<WeaponEvolutionRecipe> OnEvolutionReady;

        private readonly HashSet<WeaponEvolutionRecipe> _readyLastTick = new HashSet<WeaponEvolutionRecipe>();
        private bool _levelUpHooked;
        private Action _levelUpHandler;
        private static bool _bootstrapDone;
        private static readonly HashSet<string> _warnedOnce = new HashSet<string>();

        private EvolutionService() { }

        /// <summary>Called automatically on first scene load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapDone) return;
            _bootstrapDone = true;
            try { Instance.HookLevelUp(); }
            catch (Exception e) { Debug.LogWarning("[EvolutionService] Bootstrap failed: " + e.Message); }
        }

        /// <summary>Public re-hook for tests / scene reloads.</summary>
        public void Rehook()
        {
            _levelUpHooked = false;
            HookLevelUp();
        }

        private void HookLevelUp()
        {
            if (_levelUpHooked) return;
            _levelUpHandler = HandleLevelUp;

            var playerLevelType = FindTypeByName("PlayerLevel");
            if (playerLevelType == null)
            {
                WarnOnce("PlayerLevel-missing",
                    "[EvolutionService] PlayerLevel type not found; evolutions disabled.");
                return;
            }

            // Try static event OnLevelUp (Action).
            var evt = playerLevelType.GetEvent("OnLevelUp",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            if (evt != null)
            {
                try
                {
                    var handler = BuildCompatibleDelegate(evt.EventHandlerType, _levelUpHandler);
                    if (handler != null)
                    {
                        if (evt.GetAddMethod(true).IsStatic)
                            evt.AddEventHandler(null, handler);
                        else
                        {
                            var inst = FindLivingInstance(playerLevelType);
                            if (inst != null) evt.AddEventHandler(inst, handler);
                            else WarnOnce("PlayerLevel-instance",
                                "[EvolutionService] PlayerLevel.OnLevelUp is non-static and no live instance found.");
                        }
                        _levelUpHooked = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    WarnOnce("PlayerLevel-evt",
                        "[EvolutionService] Could not subscribe to PlayerLevel.OnLevelUp: " + e.Message);
                }
            }

            WarnOnce("PlayerLevel-noevent",
                "[EvolutionService] PlayerLevel found but no OnLevelUp event; evolutions disabled.");
        }

        private static Delegate BuildCompatibleDelegate(Type targetDelegateType, Action sink)
        {
            // Wrap our parameterless Action into whatever the event's delegate is.
            var invoke = targetDelegateType.GetMethod("Invoke");
            if (invoke == null) return null;
            var parameters = invoke.GetParameters();

            if (parameters.Length == 0 && invoke.ReturnType == typeof(void))
                return Delegate.CreateDelegate(targetDelegateType, sink.Target, sink.Method, throwOnBindFailure: false);

            // For Action<T> (e.g. level int) - generate a lambda via DynamicMethod-free path:
            // we use a closure-helper.
            if (invoke.ReturnType == typeof(void) && parameters.Length == 1)
                return new EvolutionLevelUpAdapter(sink).MakeDelegate(targetDelegateType);

            return null;
        }

        private static UnityEngine.Object FindLivingInstance(Type t)
        {
            if (!typeof(UnityEngine.Object).IsAssignableFrom(t)) return null;
            var found = UnityEngine.Object.FindObjectsByType(t,
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found != null && found.Length > 0 ? (UnityEngine.Object)found[0] : null;
        }

        private void HandleLevelUp()
        {
            try { EvaluateRecipes(raiseAvailableOnSatisfied: true); }
            catch (Exception e) { Debug.LogWarning("[EvolutionService] LevelUp eval failed: " + e.Message); }
        }

        /// <summary>
        /// Public hook so other systems (e.g. UpgradeService) can ping the service after a
        /// passive is picked - lets the READY-TO-EVOLVE badge appear immediately rather
        /// than waiting for the next level-up.
        /// </summary>
        public void RefreshReadyState()
        {
            try { EvaluateRecipes(raiseAvailableOnSatisfied: false); }
            catch (Exception e) { Debug.LogWarning("[EvolutionService] Refresh failed: " + e.Message); }
        }

        private void EvaluateRecipes(bool raiseAvailableOnSatisfied)
        {
            var registry = EvolutionRecipeRegistry.Instance;
            if (registry == null || registry.Recipes == null) return;

            var nowReady = new HashSet<WeaponEvolutionRecipe>();
            foreach (var recipe in registry.Recipes)
            {
                if (recipe == null || !recipe.IsValid()) continue;
                if (!IsRecipeSatisfied(recipe)) continue;

                nowReady.Add(recipe);
                if (!_readyLastTick.Contains(recipe))
                {
                    try { OnEvolutionReady?.Invoke(recipe); }
                    catch (Exception e) { Debug.LogWarning("[EvolutionService] OnEvolutionReady threw: " + e.Message); }
                }

                if (raiseAvailableOnSatisfied)
                {
                    try { OnEvolutionAvailable?.Invoke(recipe); }
                    catch (Exception e) { Debug.LogWarning("[EvolutionService] OnEvolutionAvailable threw: " + e.Message); }
                }
            }

            _readyLastTick.Clear();
            foreach (var r in nowReady) _readyLastTick.Add(r);
        }

        /// <summary>
        /// Returns true when the base weapon is in the inventory at max rank AND the
        /// required passive has at least one stack.
        /// </summary>
        public bool IsRecipeSatisfied(WeaponEvolutionRecipe recipe)
        {
            if (recipe == null || !recipe.IsValid()) return false;
            return WeaponMaxedInInventory(recipe.BaseWeapon)
                && PassiveOwned(recipe.RequiredPassiveUpgradeName);
        }

        // ---------------- Inventory / upgrade reflection wrappers ----------------

        private static bool WeaponMaxedInInventory(ScriptableObject weapon)
        {
            if (weapon == null) return false;
            var invType = FindTypeByName("WeaponInventory");
            if (invType == null) { WarnOnce("WeaponInventory-missing",
                "[EvolutionService] WeaponInventory type not found."); return false; }
            var inv = FindLivingInstance(invType);
            if (inv == null) return false;

            int rank = InvokeIntMethod(invType, inv, "GetWeaponRank", weapon, defaultValue: -1);
            int max = InvokeIntMethod(invType, inv, "GetMaxRank", weapon, defaultValue: -1);

            if (rank < 0)
            {
                bool has = InvokeBoolMethod(invType, inv, "HasWeapon", weapon, defaultValue: false);
                if (!has) return false;
                // Fall back to "owned == satisfied" if rank introspection is unavailable.
                if (max < 0) return true;
            }
            return rank >= 0 && max > 0 && rank >= max;
        }

        private static bool PassiveOwned(string upgradeName)
        {
            if (string.IsNullOrWhiteSpace(upgradeName)) return false;
            var svcType = FindTypeByName("UpgradeService");
            if (svcType == null) { WarnOnce("UpgradeService-missing",
                "[EvolutionService] UpgradeService type not found."); return false; }
            var svc = FindLivingInstance(svcType);
            if (svc == null)
            {
                // Singleton-style accessor?
                var prop = svcType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (prop != null) svc = prop.GetValue(null) as UnityEngine.Object;
            }
            if (svc == null) return false;

            int count = InvokeIntMethodStrName(svcType, svc, "GetStackCount", upgradeName, defaultValue: -1);
            if (count >= 0) return count > 0;
            return InvokeBoolMethodStrName(svcType, svc, "HasUpgrade", upgradeName, defaultValue: false);
        }

        // ---------------- Apply ----------------

        /// <summary>
        /// Removes the base weapon from the inventory and grants the evolved weapon.
        ///
        /// LIMITATION: Some inventory implementations do not expose a public RemoveWeapon
        /// API. In that case we still grant the evolved weapon but the base weapon may
        /// remain active (firing alongside its evolution). The UI calls
        /// <see cref="DidRemoveBaseWeapon"/> after Apply to surface this to the player.
        /// </summary>
        public bool ApplyEvolution(WeaponEvolutionRecipe recipe)
        {
            if (recipe == null || !recipe.IsValid()) return false;
            DidRemoveBaseWeapon = false;

            var invType = FindTypeByName("WeaponInventory");
            if (invType == null) return false;
            var inv = FindLivingInstance(invType);
            if (inv == null) return false;

            DidRemoveBaseWeapon = InvokeBoolMethod(invType, inv, "RemoveWeapon", recipe.BaseWeapon, defaultValue: false);
            // If RemoveWeapon returns void/missing, try fire-and-forget variants:
            if (!DidRemoveBaseWeapon)
                TryInvokeVoidMethod(invType, inv, "RemoveWeapon", recipe.BaseWeapon);

            TryInvokeVoidMethod(invType, inv, "GrantWeapon", recipe.EvolvedWeapon);

            _readyLastTick.Remove(recipe);
            return true;
        }

        /// <summary>True iff the most recent <see cref="ApplyEvolution"/> call removed the base weapon successfully.</summary>
        public bool DidRemoveBaseWeapon { get; private set; }

        // ---------------- Reflection helpers ----------------

        private static Type FindTypeByName(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;
                foreach (var t in types)
                {
                    if (t != null && t.Name == simpleName) return t;
                }
            }
            return null;
        }

        private static int InvokeIntMethod(Type t, object target, string name, object arg, int defaultValue)
        {
            try
            {
                var m = FindMethod(t, name, arg?.GetType());
                if (m == null) return defaultValue;
                var r = m.Invoke(target, new[] { arg });
                if (r is int i) return i;
            }
            catch { /* swallow */ }
            return defaultValue;
        }

        private static int InvokeIntMethodStrName(Type t, object target, string name, string arg, int defaultValue)
        {
            try
            {
                var m = FindMethod(t, name, typeof(string));
                if (m == null) return defaultValue;
                var r = m.Invoke(target, new object[] { arg });
                if (r is int i) return i;
            }
            catch { /* swallow */ }
            return defaultValue;
        }

        private static bool InvokeBoolMethod(Type t, object target, string name, object arg, bool defaultValue)
        {
            try
            {
                var m = FindMethod(t, name, arg?.GetType());
                if (m == null) return defaultValue;
                var r = m.Invoke(target, new[] { arg });
                if (r is bool b) return b;
            }
            catch { /* swallow */ }
            return defaultValue;
        }

        private static bool InvokeBoolMethodStrName(Type t, object target, string name, string arg, bool defaultValue)
        {
            try
            {
                var m = FindMethod(t, name, typeof(string));
                if (m == null) return defaultValue;
                var r = m.Invoke(target, new object[] { arg });
                if (r is bool b) return b;
            }
            catch { /* swallow */ }
            return defaultValue;
        }

        private static void TryInvokeVoidMethod(Type t, object target, string name, object arg)
        {
            try
            {
                var m = FindMethod(t, name, arg?.GetType());
                m?.Invoke(target, new[] { arg });
            }
            catch { /* swallow */ }
        }

        private static MethodInfo FindMethod(Type t, string name, Type preferredArgType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            // Prefer an exact-arity, assignable-parameter match.
            foreach (var m in t.GetMethods(flags))
            {
                if (m.Name != name) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1) continue;
                if (preferredArgType == null) return m;
                if (ps[0].ParameterType.IsAssignableFrom(preferredArgType)) return m;
            }
            // Fall back to first by name.
            foreach (var m in t.GetMethods(flags))
            {
                if (m.Name == name && m.GetParameters().Length == 1) return m;
            }
            return null;
        }

        private static void WarnOnce(string key, string message)
        {
            if (_warnedOnce.Add(key)) Debug.LogWarning(message);
        }

        /// <summary>
        /// Tiny adapter so we can subscribe a parameterless Action to an Action&lt;T&gt; event
        /// without needing the source-generated EventHandler types.
        /// </summary>
        private sealed class EvolutionLevelUpAdapter
        {
            private readonly Action _sink;
            public EvolutionLevelUpAdapter(Action sink) { _sink = sink; }

            // Generic catch-all handler.
            // ReSharper disable once UnusedMember.Local
            public void Handle<T>(T _) { _sink?.Invoke(); }

            public Delegate MakeDelegate(Type targetDelegateType)
            {
                var invoke = targetDelegateType.GetMethod("Invoke");
                var argType = invoke.GetParameters()[0].ParameterType;
                var generic = typeof(EvolutionLevelUpAdapter).GetMethod(nameof(Handle),
                    BindingFlags.Public | BindingFlags.Instance).MakeGenericMethod(argType);
                return Delegate.CreateDelegate(targetDelegateType, this, generic, throwOnBindFailure: false);
            }
        }
    }
}
