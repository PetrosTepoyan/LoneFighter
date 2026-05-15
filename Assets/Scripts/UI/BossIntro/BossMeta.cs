using UnityEngine;

namespace LoneFighter.UI.BossIntro
{
    /// <summary>
    /// ScriptableObject describing how a single boss should present itself in the
    /// cinematic intro: which enemy it matches (by <c>EnemyData.displayName</c>),
    /// the on-screen title / subtitle, an accent colour for the banner, and an
    /// optional stinger that plays when the banner appears.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="enemyDataName"/> is the unique key. It is matched case-sensitively
    /// against <c>LoneFighter.Data.EnemyData.displayName</c> as seen on
    /// <c>EnemyBase.Data.displayName</c> by <see cref="BossIntroController"/>.
    /// </para>
    /// <para>
    /// The asset itself does not perform any sequencing — it is purely data. The
    /// orchestration lives in <see cref="BossIntroController"/> and
    /// <see cref="BossIntroSequence"/>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "LoneFighter/UI/Boss Intro/Boss Meta", fileName = "BossMeta")]
    public class BossMeta : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Must match LoneFighter.Data.EnemyData.displayName exactly (case-sensitive).")]
        public string enemyDataName = "Warden";

        [Header("Banner Text")]
        [Tooltip("Big banner title, e.g. \"WARDEN\".")]
        public string title = "WARDEN";

        [Tooltip("Smaller subtitle, e.g. \"Keeper of the Arena\".")]
        public string subtitle = "Keeper of the Arena";

        [Header("Style")]
        [Tooltip("Accent colour applied to the banner background, glow, and underline.")]
        public Color accentColor = new Color(0.55f, 0.20f, 0.85f, 1f);

        [Header("Audio")]
        [Tooltip("Optional one-shot stinger played the moment the banner slides in.")]
        public AudioClip stinger;
    }
}
