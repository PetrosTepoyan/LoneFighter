using UnityEngine;

namespace LoneFighter.Effects.CameraJuice
{
    /// <summary>
    /// Singleton facade over all the camera-juice modules in this folder.
    /// Other systems call methods here — never the modules directly — so that
    /// re-tuning the "expensive feel" of a moment is a one-file edit.
    ///
    /// Hook-up:
    ///   - Create a "CameraJuice" GameObject in the Game scene.
    ///   - Attach this script + any subset of:
    ///       <see cref="CameraPunchZoom"/>, <see cref="CameraKick"/>,
    ///       <see cref="CameraRecoil"/>, <see cref="ChromaticBurst"/>,
    ///       <see cref="RadialBlurPulse"/>, <see cref="MotionTrails"/>.
    ///   - Each module auto-resolves its <c>CinemachineCamera</c> via
    ///     <c>FindFirstObjectByType</c>, so a single Game-scene CinemachineCamera
    ///     just works. To target a different camera, drop it into the module's
    ///     inspector.
    ///
    /// All entry points are tolerant of missing modules — if a particular layer
    /// isn't present, the call no-ops.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraJuiceService : MonoBehaviour
    {
        public static CameraJuiceService Instance { get; private set; }

        [Header("Module References (auto-resolved at Start)")]
        [SerializeField] private CameraPunchZoom punchZoom;
        [SerializeField] private CameraKick kick;
        [SerializeField] private CameraRecoil recoil;
        [SerializeField] private ChromaticBurst chromatic;
        [SerializeField] private RadialBlurPulse radialBlur;
        [SerializeField] private MotionTrails motionTrails;

        [Header("Defaults")]
        [Tooltip("How long motion trails run on big moments (boss spawn, victory).")]
        [SerializeField, Min(0.05f)] private float motionTrailBurstSeconds = 0.6f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Resolve any unassigned modules via singleton or sibling lookup.
            if (punchZoom == null) punchZoom = CameraPunchZoom.Instance != null ? CameraPunchZoom.Instance : GetComponent<CameraPunchZoom>();
            if (kick == null) kick = CameraKick.Instance != null ? CameraKick.Instance : GetComponent<CameraKick>();
            if (recoil == null) recoil = CameraRecoil.Instance != null ? CameraRecoil.Instance : GetComponent<CameraRecoil>();
            if (chromatic == null) chromatic = ChromaticBurst.Instance != null ? ChromaticBurst.Instance : GetComponent<ChromaticBurst>();
            if (radialBlur == null) radialBlur = RadialBlurPulse.Instance != null ? RadialBlurPulse.Instance : GetComponent<RadialBlurPulse>();
            if (motionTrails == null) motionTrails = MotionTrails.Instance != null ? MotionTrails.Instance : GetComponent<MotionTrails>();

            if (punchZoom == null) punchZoom = FindFirstObjectByType<CameraPunchZoom>();
            if (kick == null) kick = FindFirstObjectByType<CameraKick>();
            if (recoil == null) recoil = FindFirstObjectByType<CameraRecoil>();
            if (chromatic == null) chromatic = FindFirstObjectByType<ChromaticBurst>();
            if (radialBlur == null) radialBlur = FindFirstObjectByType<RadialBlurPulse>();
            if (motionTrails == null) motionTrails = FindFirstObjectByType<MotionTrails>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // -------------------------------------------------------------------
        // Public facade
        // -------------------------------------------------------------------

        /// <summary>Player just leveled up — small punch + chromatic pulse.</summary>
        public void OnLevelUp()
        {
            if (punchZoom != null) punchZoom.PunchSmall();
            if (chromatic != null) chromatic.Pulse(0.45f);
        }

        /// <summary>A boss has just spawned — medium punch, big chroma, lens flex, motion smear.</summary>
        public void OnBossSpawn()
        {
            if (punchZoom != null) punchZoom.PunchBossSpawn();
            if (chromatic != null) chromatic.PulseBossSpawn();
            if (radialBlur != null) radialBlur.PulseBossSpawn();
            if (motionTrails != null) motionTrails.Burst(motionTrailBurstSeconds);
        }

        /// <summary>A boss has just died — heavy punch (slightly heavier than spawn).</summary>
        public void OnBossDeath()
        {
            if (punchZoom != null) punchZoom.PunchBossDeath();
            if (chromatic != null) chromatic.Pulse(0.8f);
            if (radialBlur != null) radialBlur.PulseExplosion(1f);
            if (motionTrails != null) motionTrails.Burst(motionTrailBurstSeconds);
        }

        /// <summary>An explosion went off; <paramref name="intensity"/> scales the response 0..1+.</summary>
        public void OnExplosion(float intensity = 1f)
        {
            float scaled = Mathf.Clamp(intensity, 0f, 2f);
            if (punchZoom != null) punchZoom.Punch(0.3f * scaled, 0.3f);
            if (chromatic != null) chromatic.PulseExplosion(scaled);
            if (radialBlur != null) radialBlur.PulseExplosion(scaled);
        }

        /// <summary>Player took damage from a given incoming direction (unit-ish vector).</summary>
        public void OnDamage(Vector2 incomingDir)
        {
            if (kick != null) kick.KickFromDamage(incomingDir);
        }

        /// <summary>Player fired a weapon in <paramref name="fireDir"/> — gentle opposite kick.</summary>
        public void OnFire(Vector2 fireDir)
        {
            if (kick != null) kick.KickFromFire(fireDir);
        }

        /// <summary>Continuous weapon emitting this frame (Flamethrower, beam, etc.).</summary>
        public void OnContinuousFire()
        {
            if (recoil != null) recoil.Rumble();
        }

        /// <summary>Continuous weapon emitting this frame at custom amplitude.</summary>
        public void OnContinuousFire(float amplitude)
        {
            if (recoil != null) recoil.Rumble(amplitude);
        }

        /// <summary>Run win — heavy punch, motion smear, big chroma pulse.</summary>
        public void OnVictory()
        {
            if (punchZoom != null) punchZoom.PunchBossDeath();
            if (chromatic != null) chromatic.Pulse(0.9f);
            if (motionTrails != null) motionTrails.Burst(motionTrailBurstSeconds * 1.5f);
        }
    }
}
