using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Systems
{
    /// Central audio bus. Two responsibilities:
    ///   1) Legacy direct-clip API (PlayMusic / PlaySfx) kept for callers that hold their own AudioClip refs.
    ///   2) Cue-driven API (Play / PlayMusicCue / StopMusic) so gameplay code just says
    ///      `AudioManager.Instance.Play(AudioCue.EnemyHit)` without caring which clip backs it.
    /// Bindings (clip, volume, pitch jitter per cue) live on the component itself.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        [Header("Cue bindings (drag clips per cue, set per-clip volume and pitch jitter)")]
        [SerializeField] private List<AudioCueBinding> cueBindings = new List<AudioCueBinding>();

        /// Default pitch jitter applied when a binding has pitchJitter == (0, 0).
        [SerializeField] private Vector2 defaultPitchJitter = new Vector2(0.93f, 1.07f);

        // Cached lookup, built lazily on first use and rebuilt when bindings change at runtime.
        private Dictionary<AudioCue, AudioCueBinding> _bindingMap;
        private bool _bindingMapDirty = true;

        // Music fade state — single coroutine, cancel-safe by tracking the active handle.
        private Coroutine _musicFadeRoutine;
        private AudioCue _currentMusicCue;
        private bool _hasCurrentMusicCue;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (musicSource != null)
            {
                musicSource.loop = true;
                musicSource.volume = musicVolume;
            }
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            // Bindings list edited in inspector — force rebuild on next lookup.
            _bindingMapDirty = true;
        }

        // -----------------------------------------------------------------------------------------
        // Legacy direct-clip API (do not remove — already part of the public surface)
        // -----------------------------------------------------------------------------------------

        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null) return;
            musicSource.clip = clip;
            musicSource.volume = musicVolume;
            musicSource.pitch = 1f;
            musicSource.Play();
            _hasCurrentMusicCue = false;
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        // -----------------------------------------------------------------------------------------
        // Cue-driven API
        // -----------------------------------------------------------------------------------------

        /// Play any non-music cue as a one-shot, or set the music source's clip for music cues.
        /// Pitch jitter is applied to the sfx source; the music source stays at pitch 1 for music cues.
        /// Zero allocations on the hot path (no LINQ, no boxing).
        public void Play(AudioCue cue, float volumeScale = 1f)
        {
            if (!TryGetBinding(cue, out var binding) || binding.clip == null) return;

            if (IsMusicCue(cue))
            {
                if (musicSource == null) return;
                musicSource.clip = binding.clip;
                musicSource.volume = musicVolume * (binding.volume > 0f ? binding.volume : 1f) * Mathf.Max(0f, volumeScale);
                musicSource.pitch = 1f;
                musicSource.Play();
                _currentMusicCue = cue;
                _hasCurrentMusicCue = true;
                return;
            }

            if (sfxSource == null) return;

            // Pitch jitter — set on the source briefly; PlayOneShot inherits the source's current pitch.
            var jitter = binding.pitchJitter;
            if (Mathf.Approximately(jitter.x, 0f) && Mathf.Approximately(jitter.y, 0f))
            {
                jitter = defaultPitchJitter;
            }
            float pitch = (Mathf.Approximately(jitter.x, jitter.y))
                ? jitter.x
                : Random.Range(jitter.x, jitter.y);
            sfxSource.pitch = pitch <= 0f ? 1f : pitch;

            float clipVolume = (binding.volume > 0f ? binding.volume : 1f);
            sfxSource.PlayOneShot(binding.clip, sfxVolume * clipVolume * Mathf.Max(0f, volumeScale));
        }

        /// Crossfade from whatever's playing on the music source to the given cue's clip.
        /// Safe to call back-to-back — any in-flight fade is cancelled before starting the new one.
        public void PlayMusicCue(AudioCue cue, float fadeSeconds = 0.5f)
        {
            if (!IsMusicCue(cue))
            {
                // Treat as a fire-and-forget one-shot if caller misroutes a non-music cue here.
                Play(cue);
                return;
            }
            if (musicSource == null) return;
            if (!TryGetBinding(cue, out var binding) || binding.clip == null) return;

            CancelMusicFade();
            _musicFadeRoutine = StartCoroutine(CoFadeToMusic(binding, cue, Mathf.Max(0f, fadeSeconds)));
        }

        /// Fade-out and stop the music source. Safe to call when nothing is playing.
        public void StopMusic(float fadeSeconds = 0.5f)
        {
            if (musicSource == null) return;
            CancelMusicFade();
            _musicFadeRoutine = StartCoroutine(CoFadeOutMusic(Mathf.Max(0f, fadeSeconds)));
        }

        // -----------------------------------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------------------------------

        // Cached set of music cues, computed once via reflection so it stays in sync with
        // any future "Music*" enum entries without a per-call ToString() allocation.
        private static readonly HashSet<AudioCue> _musicCues = BuildMusicCueSet();

        private static HashSet<AudioCue> BuildMusicCueSet()
        {
            var set = new HashSet<AudioCue>();
            foreach (var name in System.Enum.GetNames(typeof(AudioCue)))
            {
                if (name.StartsWith("Music", System.StringComparison.Ordinal))
                {
                    set.Add((AudioCue)System.Enum.Parse(typeof(AudioCue), name));
                }
            }
            return set;
        }

        private static bool IsMusicCue(AudioCue cue) => _musicCues.Contains(cue);

        private bool TryGetBinding(AudioCue cue, out AudioCueBinding binding)
        {
            if (_bindingMapDirty || _bindingMap == null)
            {
                RebuildBindingMap();
            }
            return _bindingMap.TryGetValue(cue, out binding);
        }

        private void RebuildBindingMap()
        {
            if (_bindingMap == null) _bindingMap = new Dictionary<AudioCue, AudioCueBinding>(cueBindings.Count);
            else _bindingMap.Clear();

            for (int i = 0; i < cueBindings.Count; i++)
            {
                var b = cueBindings[i];
                // Last write wins on duplicate cue keys — keep authoring forgiving.
                _bindingMap[b.cue] = b;
            }
            _bindingMapDirty = false;
        }

        private void CancelMusicFade()
        {
            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
                _musicFadeRoutine = null;
            }
        }

        private IEnumerator CoFadeToMusic(AudioCueBinding binding, AudioCue cue, float fadeSeconds)
        {
            float targetVolume = musicVolume * (binding.volume > 0f ? binding.volume : 1f);

            // Fade out current track if one is playing.
            if (musicSource.isPlaying && fadeSeconds > 0f)
            {
                float startVol = musicSource.volume;
                float t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeSeconds);
                    yield return null;
                }
            }

            musicSource.Stop();
            musicSource.clip = binding.clip;
            musicSource.loop = true;
            musicSource.pitch = 1f;
            musicSource.volume = 0f;
            musicSource.Play();
            _currentMusicCue = cue;
            _hasCurrentMusicCue = true;

            // Fade in.
            if (fadeSeconds > 0f)
            {
                float t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeSeconds);
                    yield return null;
                }
            }
            musicSource.volume = targetVolume;
            _musicFadeRoutine = null;
        }

        private IEnumerator CoFadeOutMusic(float fadeSeconds)
        {
            if (!musicSource.isPlaying)
            {
                _musicFadeRoutine = null;
                yield break;
            }

            float startVol = musicSource.volume;
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeSeconds);
                yield return null;
            }
            musicSource.Stop();
            musicSource.volume = startVol; // restore so next Play uses configured volume
            _hasCurrentMusicCue = false;
            _musicFadeRoutine = null;
        }
    }
}
