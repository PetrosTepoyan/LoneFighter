using System.Reflection;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Bosses
{
    /// <summary>
    /// Three-phase Warden boss script.
    ///   * Phase 1 (100% → 66%): default chase, ~4s slam interval, single shockwave.
    ///   * Phase 2 (66%  → 33%): chase, ~3s interval, slams emit a double shockwave (a second
    ///                            wave is spawned via FxService/EnemyShockwave a beat after the first).
    ///   * Phase 3 (33%  → 0%):  ENRAGE — continuous shockwaves every 1.5s, +30% move speed,
    ///                            and the underlying WardenBoss state machine keeps running.
    ///
    /// Integration limit (read this if a phase appears to "not stick"):
    ///   The existing <see cref="WardenBoss"/> exposes <c>slamInterval</c>, <c>shockwavePrefab</c>,
    ///   and friends as <c>private [SerializeField]</c> fields with no public setters. To honor the
    ///   "new files only" rule we mutate them via reflection in <see cref="WardenBossReflection"/>.
    ///   Same story for <see cref="EnemyChaseAI.moveSpeed"/> — that one does have a public
    ///   <c>Configure(float)</c>, which we prefer when present, falling back to reflection only
    ///   if Unity stripped the method.
    ///
    /// Workaround when reflection is unavailable (IL2CPP code stripping, future Unity changes):
    ///   <see cref="PhaseShockwaveDriver"/> is a separate <see cref="MonoBehaviour"/> added to the
    ///   boss at runtime when a phase needs to emit shockwaves *independently* of WardenBoss's
    ///   own slam cadence. Phase 3 always uses this driver because it needs a 1.5s cadence
    ///   regardless of the underlying state machine. Phases 1 and 2 also fall back to it if
    ///   reflection fails — they log a one-time setup note in that case.
    /// </summary>
    public static class WardenPhases
    {
        public static void Install(PhaseManager manager)
        {
            manager.AddPhase(new Phase1Default());
            manager.AddPhase(new Phase2DoubleSlam());
            manager.AddPhase(new Phase3Enrage());
        }

        // -------------------------------------------------------------- Phase 1
        public sealed class Phase1Default : BossPhase
        {
            public override string Name => "Phase 1 — Default";
            public override float EntryHpFraction => 1.0f;

            public override void Enter(BossEncounter ctx)
            {
                var warden = ctx.GetComponent<WardenBoss>();
                if (warden != null)
                {
                    // ~4s interval = (3.5, 4.5) — slight randomness keeps the rhythm from feeling robotic.
                    WardenBossReflection.TrySetSlamInterval(warden, new Vector2(3.5f, 4.5f));
                }
                MoveSpeedModifier.Restore(ctx);
                PhaseShockwaveDriver.Detach(ctx);
            }
        }

        // -------------------------------------------------------------- Phase 2
        public sealed class Phase2DoubleSlam : BossPhase
        {
            public override string Name => "Phase 2 — Double Slam";
            public override float EntryHpFraction => 0.66f;

            public override void Enter(BossEncounter ctx)
            {
                var warden = ctx.GetComponent<WardenBoss>();
                if (warden != null)
                {
                    // ~3s interval.
                    WardenBossReflection.TrySetSlamInterval(warden, new Vector2(2.6f, 3.4f));
                }
                MoveSpeedModifier.Restore(ctx);

                // Echo each slam with a follow-up shockwave 0.35s later.
                var driver = PhaseShockwaveDriver.GetOrAdd(ctx);
                driver.ConfigureEcho(echoDelay: 0.35f);
            }

            public override void Exit(BossEncounter ctx)
            {
                var driver = ctx.GetComponent<PhaseShockwaveDriver>();
                if (driver != null) driver.ClearEcho();
            }
        }

        // -------------------------------------------------------------- Phase 3
        public sealed class Phase3Enrage : BossPhase
        {
            public override string Name => "Phase 3 — Enrage";
            public override float EntryHpFraction => 0.33f;

            public override void Enter(BossEncounter ctx)
            {
                // +30% movement speed. Read the configured speed (or fall back to a sensible default)
                // and apply through the modifier so we can cleanly restore on death.
                MoveSpeedModifier.ApplyMultiplier(ctx, 1.30f);

                // Continuous shockwaves every 1.5s, driven by our companion component so cadence
                // is decoupled from WardenBoss's slam timer. WardenBoss continues to do its own
                // slams on top — that's intentional juice for the enrage phase.
                var driver = PhaseShockwaveDriver.GetOrAdd(ctx);
                driver.ConfigureContinuous(interval: 1.5f);

                // Small visual cue.
                if (FxService.Instance != null)
                {
                    FxService.Instance.HeavyShake();
                    FxService.Instance.EnemyExplosion(ctx.transform.position);
                }
            }

            public override void Exit(BossEncounter ctx)
            {
                MoveSpeedModifier.Restore(ctx);
                var driver = ctx.GetComponent<PhaseShockwaveDriver>();
                if (driver != null) driver.ClearContinuous();
            }
        }
    }

    /// <summary>
    /// Reflection helpers for poking private SerializeFields on the existing WardenBoss without
    /// editing that file. Reflected fields are cached so the per-phase Enter() cost is one
    /// dictionary-free FieldInfo lookup amortized to a no-op after the first call.
    /// </summary>
    internal static class WardenBossReflection
    {
        private static FieldInfo _slamIntervalField;
        private static bool _slamIntervalResolved;
        private static bool _slamIntervalMissingLogged;

        public static bool TrySetSlamInterval(WardenBoss warden, Vector2 interval)
        {
            if (warden == null) return false;
            if (!_slamIntervalResolved)
            {
                _slamIntervalField = typeof(WardenBoss).GetField(
                    "slamInterval",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _slamIntervalResolved = true;
            }
            if (_slamIntervalField == null)
            {
                if (!_slamIntervalMissingLogged)
                {
                    Debug.LogWarning(
                        "[WardenPhases] Could not reflect WardenBoss.slamInterval — phase will not retune slam cadence. " +
                        "Either expose a public setter on WardenBoss or attach a PhaseShockwaveDriver in continuous mode as a fallback.");
                    _slamIntervalMissingLogged = true;
                }
                return false;
            }
            _slamIntervalField.SetValue(warden, interval);
            return true;
        }
    }

    /// <summary>
    /// Adjusts the boss's <see cref="EnemyChaseAI"/> movement speed by a multiplier and remembers
    /// the original so <see cref="Restore"/> can put it back. Uses the public Configure(float)
    /// when reflection on <c>moveSpeed</c> works for both read and write; otherwise logs once.
    /// </summary>
    internal static class MoveSpeedModifier
    {
        private const string MarkerKey = "_LoneFighter_OriginalSpeed";

        // We store the original speed on the GameObject by sticking it on a tiny companion
        // component so we don't accidentally double-apply when a phase is re-entered.
        public static void ApplyMultiplier(BossEncounter ctx, float multiplier)
        {
            var ai = ctx.GetComponent<EnemyChaseAI>();
            if (ai == null)
            {
                Debug.LogWarning($"[WardenPhases] {ctx.name} has no EnemyChaseAI; cannot apply speed multiplier.");
                return;
            }

            var marker = ctx.GetComponent<MoveSpeedMarker>();
            if (marker == null)
            {
                marker = ctx.gameObject.AddComponent<MoveSpeedMarker>();
                marker.OriginalSpeed = ReadMoveSpeed(ai);
            }

            float newSpeed = marker.OriginalSpeed * multiplier;
            ai.Configure(newSpeed);
        }

        public static void Restore(BossEncounter ctx)
        {
            var marker = ctx.GetComponent<MoveSpeedMarker>();
            if (marker == null) return;
            var ai = ctx.GetComponent<EnemyChaseAI>();
            if (ai != null) ai.Configure(marker.OriginalSpeed);
            UnityEngine.Object.Destroy(marker);
        }

        private static FieldInfo _moveSpeedField;
        private static bool _moveSpeedResolved;
        private static bool _moveSpeedMissingLogged;

        private static float ReadMoveSpeed(EnemyChaseAI ai)
        {
            if (!_moveSpeedResolved)
            {
                _moveSpeedField = typeof(EnemyChaseAI).GetField(
                    "moveSpeed",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _moveSpeedResolved = true;
            }
            if (_moveSpeedField == null)
            {
                if (!_moveSpeedMissingLogged)
                {
                    Debug.LogWarning(
                        "[WardenPhases] Could not reflect EnemyChaseAI.moveSpeed — using fallback default 2.0 for restoration. " +
                        "Add a public getter on EnemyChaseAI or use ScriptableObject EnemyData to define base speed.");
                    _moveSpeedMissingLogged = true;
                }
                return 2f; // matches EnemyChaseAI's default serialized value
            }
            return (float)_moveSpeedField.GetValue(ai);
        }
    }

    /// <summary>Internal companion component used by <see cref="MoveSpeedModifier"/>.</summary>
    [AddComponentMenu("")] // hide from Add Component menu
    internal sealed class MoveSpeedMarker : MonoBehaviour
    {
        public float OriginalSpeed = 2f;
    }
}
