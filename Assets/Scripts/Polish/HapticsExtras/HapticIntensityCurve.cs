using System;
using System.Reflection;
using UnityEngine;

namespace LoneFighter.Polish.HapticsExtras
{
    /// <summary>
    /// Maps a 0..1 "gameplay intensity" signal to a haptic-strength multiplier. Higher gameplay
    /// chaos → more rumble.
    ///
    /// <para><b>Intensity source</b></para>
    /// We look for a sibling Adaptive Audio system's <c>IntensityResolver</c> via reflection so
    /// we have <em>zero</em> compile-time dependency on it (the type may or may not exist on
    /// this branch). The probe:
    /// <list type="number">
    ///   <item><description>Find any type named <c>IntensityResolver</c> in any loaded assembly.</description></item>
    ///   <item><description>Try <c>Instance</c> static property/field → call <c>Current</c> or
    ///       <c>GetCurrent()</c>.</description></item>
    ///   <item><description>Fallback to a static <c>Current</c> / <c>GetCurrent()</c> /
    ///       <c>Value</c> member on the type itself.</description></item>
    ///   <item><description>If nothing matches, return a neutral 0.5 so callers get a sane
    ///       middle-of-the-road scalar instead of guessing.</description></item>
    /// </list>
    /// Lookups are cached after the first probe so this is cheap to call every frame.
    /// </summary>
    public class HapticIntensityCurve
    {
        /// <summary>Animation curve mapping intensity (x ∈ [0,1]) → strength scalar (y, typically [0,1.5]).</summary>
        public AnimationCurve Curve { get; set; }

        /// <summary>Fallback intensity when no <c>IntensityResolver</c> can be found.</summary>
        public float FallbackIntensity { get; set; } = 0.5f;

        /// <summary>Default curve: gentle ease-in so low chaos barely shakes, high chaos punches hard.</summary>
        public static AnimationCurve DefaultCurve()
        {
            // Eased: y(0)=0.4, y(0.5)=0.85, y(1)=1.25
            return new AnimationCurve(
                new Keyframe(0f, 0.4f, 0f, 1f),
                new Keyframe(0.5f, 0.85f, 1f, 1f),
                new Keyframe(1f, 1.25f, 0.5f, 0f));
        }

        public HapticIntensityCurve()
        {
            Curve = DefaultCurve();
        }

        public HapticIntensityCurve(AnimationCurve curve)
        {
            Curve = curve ?? DefaultCurve();
        }

        /// <summary>
        /// Sample the curve at the CURRENT gameplay intensity. Returns a multiplier suitable
        /// for combining with <see cref="HapticsSettings.Strength"/>.
        /// </summary>
        public float Evaluate()
        {
            float intensity = SampleIntensity();
            float y = Curve != null ? Curve.Evaluate(Mathf.Clamp01(intensity)) : intensity;
            return Mathf.Max(0f, y);
        }

        /// <summary>Sample the curve at a specific intensity value, bypassing the resolver probe.</summary>
        public float EvaluateAt(float intensity01)
        {
            float clamped = Mathf.Clamp01(intensity01);
            float y = Curve != null ? Curve.Evaluate(clamped) : clamped;
            return Mathf.Max(0f, y);
        }

        // ---- Reflection probe -------------------------------------------------------------

        private static bool _probed;
        private static Func<float> _intensityGetter;

        /// <summary>The raw 0..1 gameplay-intensity reading, or <see cref="FallbackIntensity"/>.</summary>
        public float SampleIntensity()
        {
            if (!_probed) Probe();
            try
            {
                if (_intensityGetter != null)
                {
                    return Mathf.Clamp01(_intensityGetter());
                }
            }
            catch
            {
                // Resolver disappeared / threw — fall through to default. We deliberately swallow
                // because optional dependency: this should never break the game.
            }
            return Mathf.Clamp01(FallbackIntensity);
        }

        /// <summary>Force a re-probe — useful after additive scene loads / assembly reloads.</summary>
        public static void ResetProbe()
        {
            _probed = false;
            _intensityGetter = null;
        }

        private static void Probe()
        {
            _probed = true;
            _intensityGetter = null;

            Type resolverType = null;
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length && resolverType == null; i++)
                {
                    var asm = assemblies[i];
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                    if (types == null) continue;
                    for (int t = 0; t < types.Length; t++)
                    {
                        var tt = types[t];
                        if (tt == null) continue;
                        if (tt.Name == "IntensityResolver")
                        {
                            resolverType = tt;
                            break;
                        }
                    }
                }
            }
            catch
            {
                return;
            }

            if (resolverType == null) return;

            // 1) Try singleton-style: Instance.Current / Instance.GetCurrent()
            object instance = GetMember(resolverType, "Instance", BindingFlags.Public | BindingFlags.Static);
            if (instance != null)
            {
                _intensityGetter = BuildGetterFromInstance(instance);
                if (_intensityGetter != null) return;
            }

            // 2) Static members directly on the type.
            _intensityGetter = BuildStaticGetter(resolverType);
        }

        private static Func<float> BuildGetterFromInstance(object instance)
        {
            var type = instance.GetType();
            // Property: Current
            var prop = type.GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && IsFloatish(prop.PropertyType))
            {
                return () => ToFloat(prop.GetValue(instance));
            }
            // Method: GetCurrent()
            var method = type.GetMethod("GetCurrent", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method != null && IsFloatish(method.ReturnType))
            {
                return () => ToFloat(method.Invoke(instance, null));
            }
            // Field: Current / Value
            var field = type.GetField("Current", BindingFlags.Public | BindingFlags.Instance)
                     ?? type.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && IsFloatish(field.FieldType))
            {
                return () => ToFloat(field.GetValue(instance));
            }
            return null;
        }

        private static Func<float> BuildStaticGetter(Type type)
        {
            var prop = type.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)
                    ?? type.GetProperty("Value", BindingFlags.Public | BindingFlags.Static);
            if (prop != null && IsFloatish(prop.PropertyType))
            {
                return () => ToFloat(prop.GetValue(null));
            }
            var method = type.GetMethod("GetCurrent", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method != null && IsFloatish(method.ReturnType))
            {
                return () => ToFloat(method.Invoke(null, null));
            }
            var field = type.GetField("Current", BindingFlags.Public | BindingFlags.Static)
                     ?? type.GetField("Value", BindingFlags.Public | BindingFlags.Static);
            if (field != null && IsFloatish(field.FieldType))
            {
                return () => ToFloat(field.GetValue(null));
            }
            return null;
        }

        private static object GetMember(Type type, string name, BindingFlags flags)
        {
            var prop = type.GetProperty(name, flags);
            if (prop != null) return prop.GetValue(null);
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(null);
            return null;
        }

        private static bool IsFloatish(Type t)
        {
            return t == typeof(float) || t == typeof(double) || t == typeof(int);
        }

        private static float ToFloat(object boxed)
        {
            if (boxed is float f) return f;
            if (boxed is double d) return (float)d;
            if (boxed is int i) return i;
            return 0f;
        }
    }
}
