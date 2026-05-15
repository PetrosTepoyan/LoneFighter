using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LoneFighter.Combat.Elements
{
    /// <summary>
    /// Static glue between weapons, the elemental tables, the enemy damage entry-point and the
    /// (sibling) Status system at <c>Assets/Scripts/Combat/Status/</c>.
    ///
    /// Because <c>EnemyBase</c> and the status-effect MonoBehaviours live in folders owned by
    /// other agents, we resolve them lazily via reflection. This means our assembly compiles in
    /// isolation while still wiring up correctly once the sibling code lands.
    /// </summary>
    public static class ElementalDamageDispatcher
    {
        // ---- Reflection caches ----------------------------------------------------------
        private static bool _enemyBaseResolved;
        private static Type _enemyBaseType;
        private static MethodInfo _applyDamage;

        private static readonly Dictionary<string, Type> _statusTypeCache = new Dictionary<string, Type>();

        /// <summary>
        /// Computes <c>final = baseDamage * resistance(element)</c>, calls <c>EnemyBase.ApplyDamage</c>
        /// on the enemy, then (if a sibling Status system is present) inflicts the element's status
        /// effect named by the active <see cref="ElementProfile.StatusEffectTypeName"/>.
        /// </summary>
        /// <param name="enemy">Target enemy (an <c>EnemyBase</c>-derived component or its GameObject host).</param>
        /// <param name="baseDamage">Pre-resistance damage value.</param>
        /// <param name="element">Element of the hit.</param>
        /// <param name="sourceWeapon">Weapon GameObject that produced the hit (may be null).</param>
        public static void Apply(Component enemy, float baseDamage, Element element, GameObject sourceWeapon)
        {
            if (enemy == null) return;
            Apply(enemy.gameObject, baseDamage, element, sourceWeapon, enemy);
        }

        /// <summary>GameObject overload (matches the spec signature shape).</summary>
        public static void Apply(GameObject enemyGo, float baseDamage, Element element, GameObject sourceWeapon)
        {
            if (enemyGo == null) return;
            Apply(enemyGo, baseDamage, element, sourceWeapon, enemyComponentHint: null);
        }

        private static void Apply(GameObject enemyGo, float baseDamage, Element element, GameObject sourceWeapon, Component enemyComponentHint)
        {
            float multiplier = EnemyResistance.DefaultMultiplier;
            if (enemyGo.TryGetComponent<EnemyResistance>(out var resistance))
            {
                multiplier = resistance.GetMultiplier(element);
            }

            float weaponMultiplier = 1f;
            if (sourceWeapon != null && sourceWeapon.TryGetComponent<WeaponElementBinding>(out var binding))
            {
                weaponMultiplier = binding.Multiplier;
            }

            float finalDamage = Mathf.Max(0f, baseDamage * weaponMultiplier * multiplier);

            // --- 1. Deal damage ---------------------------------------------------------
            InvokeEnemyApplyDamage(enemyGo, enemyComponentHint, finalDamage, element, sourceWeapon);

            // --- 2. Inflict status effect, if any --------------------------------------
            var profile = ElementRegistry.Get(element);
            if (profile == null) return;

            // Spawn impact VFX (optional; no-op if prefab unset).
            if (profile.ParticlePrefab != null)
            {
                UnityEngine.Object.Instantiate(profile.ParticlePrefab, enemyGo.transform.position, Quaternion.identity);
            }

            var statusName = profile.StatusEffectTypeName;
            if (string.IsNullOrEmpty(statusName)) return;

            var statusType = ResolveStatusType(statusName);
            if (statusType == null) return;

            // If the effect already exists, give it a chance to "refresh" via a public Refresh() method,
            // otherwise add a new instance. This keeps the contract loose for the sibling Status agent.
            var existing = enemyGo.GetComponent(statusType);
            if (existing != null)
            {
                var refresh = statusType.GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public);
                if (refresh != null && refresh.GetParameters().Length == 0)
                {
                    try { refresh.Invoke(existing, null); }
                    catch (Exception e) { Debug.LogWarning($"[ElementalDamageDispatcher] Refresh threw: {e.Message}"); }
                }
            }
            else
            {
                try { enemyGo.AddComponent(statusType); }
                catch (Exception e) { Debug.LogWarning($"[ElementalDamageDispatcher] AddComponent<{statusName}> failed: {e.Message}"); }
            }
        }

        // ---------------------------------------------------------------------------------
        // Reflection helpers
        // ---------------------------------------------------------------------------------

        private static void InvokeEnemyApplyDamage(GameObject enemyGo, Component hint, float damage, Element element, GameObject sourceWeapon)
        {
            ResolveEnemyBase();

            Component target = hint;
            if (target == null && _enemyBaseType != null)
            {
                target = enemyGo.GetComponent(_enemyBaseType) as Component;
            }
            if (target == null)
            {
                // Fall back to any component that exposes a public "ApplyDamage" method (duck-typed).
                var components = enemyGo.GetComponents<Component>();
                for (int i = 0; i < components.Length && target == null; i++)
                {
                    var c = components[i];
                    if (c == null) continue;
                    if (FindApplyDamage(c.GetType()) != null) target = c;
                }
            }
            if (target == null)
            {
                Debug.LogWarning($"[ElementalDamageDispatcher] No EnemyBase/ApplyDamage target on '{enemyGo.name}'.");
                return;
            }

            var method = _applyDamage != null && _enemyBaseType != null && _enemyBaseType.IsInstanceOfType(target)
                ? _applyDamage
                : FindApplyDamage(target.GetType());

            if (method == null)
            {
                Debug.LogWarning($"[ElementalDamageDispatcher] '{target.GetType().Name}' has no ApplyDamage method.");
                return;
            }

            try
            {
                var args = BuildArgs(method, damage, element, sourceWeapon);
                method.Invoke(target, args);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ElementalDamageDispatcher] ApplyDamage invoke failed: {e.Message}");
            }
        }

        private static object[] BuildArgs(MethodInfo method, float damage, Element element, GameObject sourceWeapon)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var t = p.ParameterType;
                if (t == typeof(float)) args[i] = damage;
                else if (t == typeof(int)) args[i] = Mathf.RoundToInt(damage);
                else if (t == typeof(Element)) args[i] = element;
                else if (t == typeof(GameObject)) args[i] = sourceWeapon;
                else if (typeof(Component).IsAssignableFrom(t)) args[i] = null;
                else if (p.HasDefaultValue) args[i] = p.DefaultValue;
                else args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
            }
            return args;
        }

        private static MethodInfo FindApplyDamage(Type t)
        {
            if (t == null) return null;
            // Prefer the simplest float overload.
            var m = t.GetMethod("ApplyDamage", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(float) }, null);
            if (m != null) return m;
            // Otherwise the first ApplyDamage method we find.
            foreach (var candidate in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (candidate.Name == "ApplyDamage") return candidate;
            }
            return null;
        }

        private static void ResolveEnemyBase()
        {
            if (_enemyBaseResolved) return;
            _enemyBaseResolved = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.Name != "EnemyBase") continue;
                    if (!typeof(Component).IsAssignableFrom(t)) continue;
                    _enemyBaseType = t;
                    _applyDamage = FindApplyDamage(t);
                    return;
                }
            }
        }

        private static Type ResolveStatusType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (_statusTypeCache.TryGetValue(typeName, out var cached)) return cached;

            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!typeof(Component).IsAssignableFrom(t)) continue;
                    if (t.Name == typeName || t.FullName == typeName)
                    {
                        found = t;
                        break;
                    }
                }
                if (found != null) break;
            }

            _statusTypeCache[typeName] = found;
            return found;
        }
    }
}
