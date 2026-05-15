using System;
using UnityEngine;

namespace LoneFighter.Systems
{
    /// Strongly-typed identifiers for every sound the game can ask the AudioManager to play.
    /// Cues whose name starts with "Music" are routed to the music source; everything else to sfx.
    /// Add to the bottom of the list when adding new sounds (binding order is stable in the inspector).
    public enum AudioCue
    {
        EnemyHit,
        EnemyDeath,
        PlayerHit,
        PlayerDeath,
        ProjectileFire,
        XpPickup,
        LevelUp,
        ButtonClick,
        BossSpawn,
        BossSlam,
        MusicMain
    }

    /// Inspector-friendly binding between a cue enum value and the clip it should play.
    /// `pitchJitter` is a (min, max) multiplier applied to AudioSource.pitch around 1.0
    /// for nice variety on repeated SFX. Leave it zero/zero to fall back to the AudioManager default.
    [Serializable]
    public struct AudioCueBinding
    {
        public AudioCue cue;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume;

        /// (min, max) pitch multipliers. e.g. (0.93, 1.07) means random pitch within +/- 7%.
        public Vector2 pitchJitter;
    }
}
