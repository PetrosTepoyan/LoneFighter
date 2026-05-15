// AmbientFxController.cs
// Top-level driver for ambient world FX. Owns the intensity curve and broadcasts
// it to child effects each frame. Designed to be safe when GameManager is absent
// (uses a reflection-based proxy and falls back to internal clock).

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LoneFighter.Effects.Ambient
{
    /// <summary>
    /// Base class for all ambient FX modules. Subscribes itself to the
    /// <see cref="AmbientFxController"/> found in its parents.
    /// </summary>
    public abstract class AmbientFxModule : MonoBehaviour
    {
        protected AmbientFxController Controller { get; private set; }

        protected virtual void OnEnable()
        {
            if (Controller == null)
            {
                Controller = GetComponentInParent<AmbientFxController>();
            }

            if (Controller != null)
            {
                Controller.Register(this);
            }
        }

        protected virtual void OnDisable()
        {
            if (Controller != null)
            {
                Controller.Unregister(this);
            }
        }

        /// <summary>
        /// Called once per frame by the controller with the current 0..1 intensity.
        /// </summary>
        public abstract void Tick(float intensity, float elapsedSeconds, float deltaTime);
    }

    /// <summary>
    /// Reads elapsed run time (from <c>GameManager.Instance.ElapsedSeconds</c> if
    /// available via reflection, otherwise from its own clock), maps it to a 0..1
    /// intensity over <see cref="runDuration"/>, and drives child effect modules.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbientFxController : MonoBehaviour
    {
        [Header("Run Timing")]
        [Tooltip("Total run length in seconds. Intensity reaches 1.0 at this point.")]
        [Min(1f)]
        [SerializeField] private float runDuration = 300f; // 5 minute Vampire-Survivors arc

        [Tooltip("Curve mapping normalized run progress (x) to intensity (y).")]
        [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Fallback Clock")]
        [Tooltip("If true, the controller will run its own timer when GameManager is missing.")]
        [SerializeField] private bool useFallbackClockIfNoGameManager = true;

        [Header("Debug")]
        [SerializeField] private bool logOnce = false;

        private readonly List<AmbientFxModule> modules = new List<AmbientFxModule>(8);

        // Cached reflection handles for GameManager.Instance.ElapsedSeconds
        private bool reflectionResolved;
        private bool reflectionAvailable;
        private PropertyInfo instanceProperty;
        private PropertyInfo elapsedSecondsProperty;
        private FieldInfo elapsedSecondsField;

        private float fallbackElapsed;
        private float currentIntensity;
        private float currentElapsed;

        /// <summary>Current run intensity in 0..1.</summary>
        public float Intensity => currentIntensity;

        /// <summary>Elapsed run seconds used to derive intensity.</summary>
        public float ElapsedSeconds => currentElapsed;

        /// <summary>Configured run duration in seconds.</summary>
        public float RunDuration => runDuration;

        /// <summary>Fired each frame after intensity is computed. (intensity, elapsedSeconds)</summary>
        public event Action<float, float> OnIntensityUpdated;

        public void Register(AmbientFxModule module)
        {
            if (module != null && !modules.Contains(module))
            {
                modules.Add(module);
            }
        }

        public void Unregister(AmbientFxModule module)
        {
            if (module != null)
            {
                modules.Remove(module);
            }
        }

        private void Awake()
        {
            ResolveReflection();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            currentElapsed = ReadElapsedSeconds(dt);

            float t = runDuration > 0f ? Mathf.Clamp01(currentElapsed / runDuration) : 0f;
            currentIntensity = Mathf.Clamp01(intensityCurve != null ? intensityCurve.Evaluate(t) : t);

            // Drive modules. Iterate by index in case a module disables itself.
            for (int i = modules.Count - 1; i >= 0; i--)
            {
                var m = modules[i];
                if (m == null)
                {
                    modules.RemoveAt(i);
                    continue;
                }
                m.Tick(currentIntensity, currentElapsed, dt);
            }

            OnIntensityUpdated?.Invoke(currentIntensity, currentElapsed);

            if (logOnce)
            {
                logOnce = false;
                Debug.Log($"[AmbientFxController] elapsed={currentElapsed:0.0}s intensity={currentIntensity:0.000} modules={modules.Count} gmAvailable={reflectionAvailable}");
            }
        }

        private float ReadElapsedSeconds(float dt)
        {
            if (reflectionAvailable)
            {
                try
                {
                    object instance = instanceProperty != null ? instanceProperty.GetValue(null, null) : null;
                    if (instance != null)
                    {
                        if (elapsedSecondsProperty != null)
                        {
                            object v = elapsedSecondsProperty.GetValue(instance, null);
                            if (v is float f) return f;
                            if (v is double d) return (float)d;
                        }
                        if (elapsedSecondsField != null)
                        {
                            object v = elapsedSecondsField.GetValue(instance);
                            if (v is float f) return f;
                            if (v is double d) return (float)d;
                        }
                    }
                }
                catch
                {
                    // Swallow; fall through to fallback clock below.
                }
            }

            if (useFallbackClockIfNoGameManager)
            {
                fallbackElapsed += dt;
                return fallbackElapsed;
            }

            return currentElapsed; // freeze
        }

        private void ResolveReflection()
        {
            if (reflectionResolved) return;
            reflectionResolved = true;

            try
            {
                Type gmType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Prefer LoneFighter.Systems.GameManager but accept any GameManager type with an Instance singleton.
                    gmType = asm.GetType("LoneFighter.Systems.GameManager", false)
                          ?? asm.GetType("GameManager", false);
                    if (gmType != null) break;
                }

                if (gmType == null) return;

                instanceProperty = gmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty == null) return;

                elapsedSecondsProperty = gmType.GetProperty("ElapsedSeconds", BindingFlags.Public | BindingFlags.Instance);
                elapsedSecondsField = gmType.GetField("ElapsedSeconds", BindingFlags.Public | BindingFlags.Instance);

                reflectionAvailable = elapsedSecondsProperty != null || elapsedSecondsField != null;
            }
            catch
            {
                reflectionAvailable = false;
            }
        }
    }
}
