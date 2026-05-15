using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Audio.Adaptive
{
    /// <summary>
    /// Runtime player for an <see cref="AdaptiveMusicProfile"/>. Instantiates one looping
    /// <see cref="AudioSource"/> per stem, scheduled to start on the same DSP timestamp so
    /// they remain phase-locked, and crossfades each source's volume every frame against
    /// the current <see cref="IntensityResolver.Intensity"/>.
    ///
    /// This is intentionally parallel to <see cref="LoneFighter.Systems.AudioManager.PlayMusicCue"/> —
    /// not a replacement. Either system can be active. If both are mixed you'll want to drop
    /// AudioManager.musicVolume or set the AudioManager music cue to a "silent" clip.
    /// </summary>
    [DisallowMultipleComponent]
    public class AdaptiveMusicController : MonoBehaviour
    {
        public static AdaptiveMusicController Instance { get; private set; }

        [Header("Track")]
        [Tooltip("Profile to play. Assign in inspector or call Play(profile) at runtime.")]
        [SerializeField] private AdaptiveMusicProfile profile;

        [Tooltip("If true, plays the assigned profile in Start().")]
        [SerializeField] private bool playOnStart = true;

        [Header("Mix")]
        [Range(0f, 1f)]
        [Tooltip("Master volume applied on top of the profile's master volume and each stem's target volume.")]
        public float controllerVolume = 1f;

        [Tooltip("Mixer group routed to every stem source. Optional — leave null to use the default bus.")]
        [SerializeField] private UnityEngine.Audio.AudioMixerGroup outputMixerGroup;

        [Header("Diagnostics (read-only)")]
        [SerializeField] private bool _playing;
        [SerializeField] private int _activeStemCount;

        public bool IsPlaying => _playing;
        public AdaptiveMusicProfile CurrentProfile => profile;

        // One runtime record per stem so we can re-evaluate fades without per-frame allocations.
        private readonly List<StemRuntime> _stems = new List<StemRuntime>(8);

        private class StemRuntime
        {
            public MusicStemDefinition def;
            public AudioSource source;
            public float currentVolume;   // smoothed, what the source is at right now
            public float targetVolume;    // where currentVolume is heading
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TeardownStems();
        }

        private void Start()
        {
            if (playOnStart && profile != null)
            {
                Play(profile);
            }
        }

        /// <summary>
        /// Begins playback of the given profile. Any previously-playing stems are torn down.
        /// Safe to call repeatedly; idempotent if called with the same profile while already playing.
        /// </summary>
        public void Play(AdaptiveMusicProfile newProfile)
        {
            if (newProfile == null)
            {
                Stop();
                return;
            }

            if (_playing && newProfile == profile) return;

            TeardownStems();
            profile = newProfile;

            if (profile.stems == null || profile.stems.Count == 0)
            {
                Debug.LogWarning($"[AdaptiveMusicController] Profile '{profile.name}' has no stems — nothing to play.", this);
                return;
            }

            // Schedule every stem to start at the same DSP time so they're sample-accurate aligned.
            // 0.1 s lookahead is plenty for Unity to spool the sources without stutter.
            double startTime = AudioSettings.dspTime + 0.1;

            for (int i = 0; i < profile.stems.Count; i++)
            {
                var def = profile.stems[i];
                if (def == null) continue;
                if (def.clip == null)
                {
                    Debug.LogWarning($"[AdaptiveMusicController] Stem '{def.name}' in '{profile.name}' has no clip — skipping.", this);
                    continue;
                }

                var go = new GameObject($"Stem_{(string.IsNullOrEmpty(def.id) ? def.name : def.id)}");
                go.transform.SetParent(transform, worldPositionStays: false);

                var src = go.AddComponent<AudioSource>();
                src.clip = def.clip;
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0f; // 2D
                src.bypassEffects = false;
                src.bypassListenerEffects = false;
                src.bypassReverbZones = false;
                src.volume = 0f;
                if (outputMixerGroup != null) src.outputAudioMixerGroup = outputMixerGroup;
                src.PlayScheduled(startTime);

                _stems.Add(new StemRuntime
                {
                    def = def,
                    source = src,
                    currentVolume = 0f,
                    targetVolume = 0f,
                });
            }

            _playing = _stems.Count > 0;
            _activeStemCount = _stems.Count;
        }

        /// <summary>Stops playback and destroys the runtime stem GameObjects.</summary>
        public void Stop()
        {
            TeardownStems();
            _playing = false;
            _activeStemCount = 0;
        }

        private void TeardownStems()
        {
            for (int i = 0; i < _stems.Count; i++)
            {
                var s = _stems[i];
                if (s == null) continue;
                if (s.source != null)
                {
                    s.source.Stop();
                    if (Application.isPlaying) Destroy(s.source.gameObject);
                    else DestroyImmediate(s.source.gameObject);
                }
            }
            _stems.Clear();
        }

        private void Update()
        {
            if (!_playing || _stems.Count == 0) return;

            float intensity = IntensityResolver.Instance != null
                ? IntensityResolver.Instance.Intensity
                : 0f;

            float profileMaster = profile != null ? profile.masterVolume : 1f;
            float master = controllerVolume * profileMaster;

            for (int i = 0; i < _stems.Count; i++)
            {
                var s = _stems[i];
                if (s == null || s.source == null || s.def == null) continue;

                bool shouldPlay = intensity >= s.def.intensityThreshold;
                float target = shouldPlay ? Mathf.Clamp01(s.def.targetVolume) * master : 0f;
                s.targetVolume = target;

                // Pick the fade time directionally so authors can have asymmetric in/out.
                bool risingTowardTarget = target > s.currentVolume;
                float fade = risingTowardTarget ? s.def.fadeInSeconds : s.def.fadeOutSeconds;

                if (fade <= 0f)
                {
                    s.currentVolume = target;
                }
                else
                {
                    // Linear fade — feels right for parallel music stems (exponential ducks too late).
                    float step = (1f / fade) * Time.unscaledDeltaTime;
                    s.currentVolume = Mathf.MoveTowards(s.currentVolume, target, step);
                }

                s.source.volume = s.currentVolume;
            }
        }

        /// <summary>
        /// Editor / debug introspection: copies the current per-stem volume snapshot into
        /// <paramref name="buffer"/>. Returns the number of entries written. Pass a list at
        /// least <see cref="StemCount"/> long to avoid reallocations.
        /// </summary>
        public int GetStemVolumes(List<(string id, float volume, float target, float threshold)> buffer)
        {
            if (buffer == null) return 0;
            buffer.Clear();
            for (int i = 0; i < _stems.Count; i++)
            {
                var s = _stems[i];
                if (s == null || s.def == null) continue;
                buffer.Add((
                    string.IsNullOrEmpty(s.def.id) ? s.def.name : s.def.id,
                    s.currentVolume,
                    s.targetVolume,
                    s.def.intensityThreshold));
            }
            return buffer.Count;
        }

        public int StemCount => _stems.Count;
    }
}
