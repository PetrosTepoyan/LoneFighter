#if UNITY_EDITOR
using UnityEngine;

namespace LoneFighter.EditorTools.Art
{
    /// <summary>
    /// Neon, saturated palette tuned for a dark arena background with heavy URP Bloom.
    /// All values are chosen to push channels close to 1.0 so additive bloom blooms strongly,
    /// while remaining mutually distinguishable for gameplay readability on small portrait screens.
    ///
    /// Pixel-art philosophy: pure, saturated colors only; outlines are near-black (not pure black,
    /// so they don't look like holes in HDR); highlights are near-white-hot.
    /// </summary>
    public static class Palette
    {
        // ---- Player ----
        /// <summary>Cyan-leaning hero blue. Reads as "good guy" against red/orange swarm.</summary>
        public static readonly Color32 PlayerBlue        = new Color32(0x33, 0xCC, 0xFF, 0xFF);
        /// <summary>Lighter inner highlight for the player body.</summary>
        public static readonly Color32 PlayerBlueLight   = new Color32(0xAA, 0xEE, 0xFF, 0xFF);
        /// <summary>Forward-facing notch/indicator color (near white-hot).</summary>
        public static readonly Color32 PlayerNotch       = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        // ---- Enemies ----
        /// <summary>Grunt red — primary swarm color.</summary>
        public static readonly Color32 EnemyRed          = new Color32(0xFF, 0x33, 0x44, 0xFF);
        /// <summary>Darker red used for armor accents on the Tank.</summary>
        public static readonly Color32 EnemyRedDark      = new Color32(0x99, 0x11, 0x22, 0xFF);
        /// <summary>Runner / Bomber body orange.</summary>
        public static readonly Color32 EnemyOrange       = new Color32(0xFF, 0x88, 0x22, 0xFF);
        /// <summary>Dasher magenta.</summary>
        public static readonly Color32 EnemyMagenta      = new Color32(0xFF, 0x22, 0xCC, 0xFF);
        /// <summary>Acid green for the Spitter — distinct from XP green via more yellow.</summary>
        public static readonly Color32 EnemyAcid         = new Color32(0xAA, 0xFF, 0x22, 0xFF);

        // ---- Boss / Elite ----
        /// <summary>Warden / mini-boss purple.</summary>
        public static readonly Color32 BossPurple        = new Color32(0xAA, 0x33, 0xFF, 0xFF);
        /// <summary>Golden core inside the Warden hex.</summary>
        public static readonly Color32 BossGold          = new Color32(0xFF, 0xDD, 0x33, 0xFF);

        // ---- Projectiles / FX ----
        /// <summary>Bright projectile yellow used as the halo around the white-hot core.</summary>
        public static readonly Color32 ProjectileYellow  = new Color32(0xFF, 0xEE, 0x33, 0xFF);

        // ---- Pickups ----
        /// <summary>XP gem green — cooler than EnemyAcid so it never reads as a hostile.</summary>
        public static readonly Color32 XpGreen           = new Color32(0x33, 0xFF, 0x88, 0xFF);
        /// <summary>Cyan inner sparkle on the XP diamond.</summary>
        public static readonly Color32 XpHighlight       = new Color32(0xCC, 0xFF, 0xFF, 0xFF);

        // ---- Utility ----
        /// <summary>Near-white core for bloom hotspots.</summary>
        public static readonly Color32 WhiteHot          = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        /// <summary>Outline color. Not pure black — keeps a hint of warmth so it doesn't look like a hole under bloom.</summary>
        public static readonly Color32 OutlineDark       = new Color32(0x0C, 0x08, 0x14, 0xFF);
        /// <summary>Fully transparent.</summary>
        public static readonly Color32 Clear             = new Color32(0x00, 0x00, 0x00, 0x00);

        /// <summary>Helper: build a Color32 with a custom alpha from an existing Color32.</summary>
        public static Color32 WithAlpha(Color32 c, byte a)
        {
            c.a = a;
            return c;
        }
    }
}
#endif
