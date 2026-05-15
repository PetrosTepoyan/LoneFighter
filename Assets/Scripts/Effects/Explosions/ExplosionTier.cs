namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Six explosion intensity tiers, ordered from smallest pop to screen-eating nuke.
    /// Each tier maps to an <see cref="ExplosionProfile"/> asset that drives particle
    /// counts, shake amplitude, hit-stop, haptics, light radius and screen flash.
    ///
    /// Designed so callers can pick a tier without knowing anything about the underlying
    /// pools or particle systems — <see cref="ExplosionService.Detonate"/> dispatches
    /// across all visual layers in parallel.
    /// </summary>
    public enum ExplosionTier
    {
        /// <summary>Grunt pop. ~16 particles, no shake, no flash.</summary>
        Tiny = 0,

        /// <summary>Standard projectile impact. Light shake, micro hit-stop.</summary>
        Small = 1,

        /// <summary>Bomber blast. Heavy shake, mid hit-stop, medium haptic.</summary>
        Medium = 2,

        /// <summary>Boss minion / barrel chain link. Adds full-screen flash overlay.</summary>
        Large = 3,

        /// <summary>Cinematic level event. Heavy shake x2, big debris field.</summary>
        Mega = 4,

        /// <summary>Boss death finisher. 400+ particles, screen white-out, triple shake.</summary>
        Nuke = 5
    }
}
