using UnityEngine;

namespace LoneFighter.Effects.Decals
{
    /// <summary>
    /// Holds the sprite pools used by <see cref="BloodSplatter"/>, <see cref="ScorchMark"/>,
    /// and <see cref="BulletHole"/>. Drop this component in the Game scene and hand-assign
    /// the generated sprites (LoneFighter → FX → Generate Decal Sprites writes them under
    /// <c>Assets/Sprites/Generated/Decals/</c>).
    ///
    /// We intentionally do NOT runtime-load from Resources here — the project uses
    /// hand-wiring everywhere else, and adding a Resources folder would alter the
    /// existing asset pipeline. If the library isn't present (or has empty arrays), the
    /// helpers no-op gracefully, so the game still runs.
    /// </summary>
    [DisallowMultipleComponent]
    public class DecalSpriteLibrary : MonoBehaviour
    {
        public static DecalSpriteLibrary Instance { get; private set; }

        [Tooltip("Random irregular blob sprites used by BloodSplatter.")]
        [SerializeField] private Sprite[] bloodSplatters;

        [Tooltip("Dark sooty rings used by ScorchMark.")]
        [SerializeField] private Sprite[] scorchMarks;

        [Tooltip("Small floor-impact marks used by BulletHole.")]
        [SerializeField] private Sprite[] bulletHoles;

        public Sprite[] BloodSplatters => bloodSplatters;
        public Sprite[] ScorchMarks    => scorchMarks;
        public Sprite[] BulletHoles    => bulletHoles;

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
        }

        /// <summary>Random pick from an array; returns null if the array is null/empty.</summary>
        public static Sprite Pick(Sprite[] pool)
        {
            if (pool == null || pool.Length == 0) return null;
            return pool[Random.Range(0, pool.Length)];
        }
    }
}
