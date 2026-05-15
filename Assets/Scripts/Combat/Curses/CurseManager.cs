// CurseManager.cs — singleton that owns the player's chosen curses for the
// current run and applies their effects when the run starts.
//
// Lifecycle:
//   1. CurseSelectionUi (or another caller) populates ActiveCurses on the
//      main menu, before GameManager.LoadGameScene is called.
//   2. We persist across scene loads (DontDestroyOnLoad) so the choice
//      survives the MainMenu → Game transition.
//   3. We subscribe to GameManager.OnStateChanged. When the state flips into
//      Playing, ApplyAll() walks the active list and dispatches each curse
//      to its handler.
//
// Integration caveats (see README in this folder):
//   * Some effects can be fully applied today (player MoveSpeed, max HP).
//   * Others (Bloodlust, Swarm, ExtraBosses) need cooperating code in the
//     existing enemy / wave systems. Those handlers do the best they can
//     in isolation and raise a public static event / flag the sibling
//     systems are expected to honour once they are wired up.

using System;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Systems;
using LoneFighter.Player;

namespace LoneFighter.Combat.Curses
{
    /// <summary>
    /// Per-run container + applier for the active <see cref="Curse"/> set.
    /// </summary>
    public class CurseManager : MonoBehaviour
    {
        // ---- Singleton ----------------------------------------------------

        public static CurseManager Instance { get; private set; }

        /// <summary>
        /// Ensures a CurseManager exists in the scene the first time anyone
        /// asks for it. Avoids requiring an authored prefab in every scene.
        /// </summary>
        public static CurseManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("CurseManager");
            return go.AddComponent<CurseManager>();
        }

        // ---- Static signals for systems that can't reference us directly ---

        /// <summary>
        /// True while a NoCrits curse is active. Sibling crit systems (e.g.
        /// CritService) should read this static flag on every roll. We use a
        /// flag rather than an event because crit rolls happen on every shot
        /// — a flag is a single load with no allocation overhead.
        /// </summary>
        public static bool NoCrits { get; private set; }

        /// <summary>
        /// Multiplicative factor weapons should fold into outgoing damage,
        /// driven primarily by GlassCannon. Defaults to 1.0 between runs.
        /// </summary>
        public static float PlayerDamageMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Multiplicative factor for enemy contact / projectile damage,
        /// driven by Bloodlust. Sibling enemy code should read this when
        /// resolving a hit. Defaults to 1.0.
        /// </summary>
        public static float EnemyDamageMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Multiplicative factor for the wave spawn rate, driven by Swarm.
        /// Defaults to 1.0. A future patch to WaveManager should read this
        /// each frame when accumulating spawn budget.
        /// </summary>
        public static float SpawnRateMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Multiplicative factor for the count of bosses spawned by
        /// WaveManager, driven by ExtraBosses. Defaults to 1.0.
        /// </summary>
        public static float BossCountMultiplier { get; private set; } = 1f;

        /// <summary>
        /// True when DenseFog is active. A VFX layer in the Game scene can
        /// watch this and toggle a heavy vignette / post-processing override.
        /// </summary>
        public static bool DenseFog { get; private set; }

        /// <summary>
        /// Raised whenever the static flags above change (typically at
        /// run-start and at run-end). Subscribers can re-pull whichever
        /// fields they care about without us defining one event per field.
        /// </summary>
        public static event Action OnEffectsChanged;

        // ---- Active set ---------------------------------------------------

        [Tooltip("Curses chosen for the current run. Populate via the " +
                 "selection UI on the main menu, or pre-populate in the " +
                 "Editor for testing.")]
        [SerializeField] private List<Curse> activeCurses = new();

        /// <summary>Read-only view of the active curse set.</summary>
        public IReadOnlyList<Curse> ActiveCurses => activeCurses;

        /// <summary>
        /// Maximum number of curses the player can select per run. Mirrored
        /// in the selection UI so it can grey out additional cards.
        /// </summary>
        public const int MaxSelectable = 3;

        // ---- Lifecycle ----------------------------------------------------

        private bool _appliedThisRun;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // GameManager creates its instance in its own Awake. If we wake
            // up first (main menu boot order), defer until it appears.
            TrySubscribe();
        }

        private void Start()
        {
            // Belt-and-braces — covers the case where GameManager.Awake ran
            // after our Awake but before our OnEnable would have been wired.
            TrySubscribe();
        }

        private bool _subscribed;
        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed && GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
            }
            _subscribed = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleStateChanged(GameState prev, GameState next)
        {
            // We apply on the transition INTO Playing from any non-Playing
            // state, but only once per run — re-entering Playing from a
            // pause or level-up modal must not re-stack the effects.
            if (next == GameState.Playing && prev != GameState.Paused && prev != GameState.LevelUp)
            {
                ApplyAll();
            }
            else if (next == GameState.GameOver || next == GameState.Victory || next == GameState.Boot)
            {
                // Clear runtime flags so the next run starts clean even if
                // the player keeps the same curse selection. Boot is the
                // state we return to when the player drops back to the
                // main menu mid-run — without a reset here, re-clicking
                // Play would skip ApplyAll on the _appliedThisRun guard.
                ResetStaticEffects();
                _appliedThisRun = false;
            }
        }

        // ---- Public mutation API -----------------------------------------

        /// <summary>
        /// Replaces the active set wholesale. Pass an empty / null list to
        /// disable all curses for the upcoming run.
        /// </summary>
        public void SetActiveCurses(IEnumerable<Curse> curses)
        {
            activeCurses.Clear();
            if (curses == null) return;
            foreach (var c in curses)
            {
                if (c == null) continue;
                if (activeCurses.Count >= MaxSelectable) break;
                activeCurses.Add(c);
            }
        }

        /// <summary>
        /// Adds a curse to the active set. No-op if the curse is null,
        /// already present, or the cap has been reached. Returns whether
        /// the operation succeeded.
        /// </summary>
        public bool AddCurse(Curse curse)
        {
            if (curse == null) return false;
            if (activeCurses.Contains(curse)) return false;
            if (activeCurses.Count >= MaxSelectable) return false;
            activeCurses.Add(curse);
            return true;
        }

        /// <summary>Removes a curse from the active set.</summary>
        public bool RemoveCurse(Curse curse)
        {
            return curse != null && activeCurses.Remove(curse);
        }

        /// <summary>Removes every curse from the active set.</summary>
        public void ClearCurses() => activeCurses.Clear();

        // ---- Apply --------------------------------------------------------

        /// <summary>
        /// Dispatches each active curse to its handler. Idempotent within a
        /// single run — guarded by <see cref="_appliedThisRun"/>.
        /// </summary>
        public void ApplyAll()
        {
            if (_appliedThisRun)
            {
                // Re-entering Playing after a pause or level-up — nothing
                // to do, effects are already applied.
                return;
            }
            _appliedThisRun = true;

            ResetStaticEffects(silent: true);

            for (int i = 0; i < activeCurses.Count; i++)
            {
                var c = activeCurses[i];
                if (c == null) continue;
                try
                {
                    ApplyOne(c);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CurseManager] Failed to apply '{c.displayName}' ({c.kind}): {ex.Message}", this);
                }
            }

            Debug.Log($"[CurseManager] Applied {activeCurses.Count} curse(s). Reward multiplier = x{CurseRewardCalculator.Multiplier:0.00}");
            OnEffectsChanged?.Invoke();
        }

        private void ApplyOne(Curse c)
        {
            switch (c.kind)
            {
                case CurseKind.Bloodlust:
                    // Best-effort: surface the multiplier as a static field
                    // and warn that no enemy code reads it yet. A future
                    // patch to EnemyBase.TryDamagePlayer should multiply
                    // contactDamage by CurseManager.EnemyDamageMultiplier.
                    EnemyDamageMultiplier *= (1f + c.magnitude);
                    Debug.LogWarning("[CurseManager] Bloodlust active: enemy damage x" +
                                     EnemyDamageMultiplier.ToString("0.00") +
                                     ". Awaiting integration in EnemyBase (see Combat/Curses/README).");
                    break;

                case CurseKind.Swarm:
                    // Same story — static factor, requires WaveManager patch.
                    SpawnRateMultiplier *= (1f + c.magnitude);
                    Debug.LogWarning("[CurseManager] Swarm active: spawn rate x" +
                                     SpawnRateMultiplier.ToString("0.00") +
                                     ". Awaiting integration in WaveManager (see Combat/Curses/README).");
                    break;

                case CurseKind.Fragile:
                    ApplyMaxHpScale(1f - c.magnitude, refill: true, label: "Fragile");
                    break;

                case CurseKind.Slowed:
                    ApplyMoveSpeedScale(1f - c.magnitude, label: "Slowed");
                    break;

                case CurseKind.NoCrits:
                    NoCrits = true;
                    Debug.Log("[CurseManager] NoCrits active: crit rolls suppressed.");
                    break;

                case CurseKind.GlassCannon:
                    PlayerDamageMultiplier *= (1f + c.magnitude);
                    ApplyMaxHpScale(1f - c.magnitude, refill: true, label: "GlassCannon");
                    Debug.Log("[CurseManager] GlassCannon active: player damage x" +
                              PlayerDamageMultiplier.ToString("0.00") +
                              ". Weapons should multiply outgoing damage by CurseManager.PlayerDamageMultiplier.");
                    break;

                case CurseKind.DenseFog:
                    DenseFog = true;
                    // CurseHud or a dedicated VFX object is responsible for
                    // toggling the vignette when it observes this flag.
                    Debug.Log("[CurseManager] DenseFog active. CurseHud will render a heavy vignette overlay.");
                    break;

                case CurseKind.ExtraBosses:
                    BossCountMultiplier *= 2f;
                    Debug.LogWarning("[CurseManager] ExtraBosses active: boss count x" +
                                     BossCountMultiplier.ToString("0.00") +
                                     ". Awaiting integration in WaveManager (see Combat/Curses/README).");
                    break;

                default:
                    Debug.LogWarning($"[CurseManager] No handler for curse kind {c.kind}");
                    break;
            }
        }

        // ---- Player-side appliers ----------------------------------------

        private static void ApplyMaxHpScale(float scale, bool refill, string label)
        {
            var pc = PlayerController.Instance;
            if (pc == null)
            {
                Debug.LogWarning($"[CurseManager] {label}: no PlayerController.Instance yet — effect skipped.");
                return;
            }
            var health = pc.GetComponent<PlayerHealth>();
            if (health == null)
            {
                Debug.LogWarning($"[CurseManager] {label}: PlayerController has no PlayerHealth — effect skipped.");
                return;
            }
            float newMax = Mathf.Max(1f, health.Max * scale);
            health.SetMax(newMax, refill);
            Debug.Log($"[CurseManager] {label}: max HP scaled by {scale:0.00} → {newMax:0}");
        }

        private static void ApplyMoveSpeedScale(float scale, string label)
        {
            var pc = PlayerController.Instance;
            if (pc == null)
            {
                Debug.LogWarning($"[CurseManager] {label}: no PlayerController.Instance yet — effect skipped.");
                return;
            }
            float before = pc.MoveSpeed;
            pc.MoveSpeed = before * scale;
            Debug.Log($"[CurseManager] {label}: move speed {before:0.00} → {pc.MoveSpeed:0.00}");
        }

        // ---- Reset --------------------------------------------------------

        /// <summary>
        /// Clears every runtime side-effect this manager owns. Called on
        /// run end (GameOver / Victory) so the static factors don't leak
        /// into the next run. Does NOT clear the chosen curse list — the
        /// player typically wants to repeat their selection.
        /// </summary>
        public void ResetStaticEffects() => ResetStaticEffects(silent: false);

        private void ResetStaticEffects(bool silent)
        {
            bool anyChange =
                NoCrits ||
                DenseFog ||
                !Mathf.Approximately(PlayerDamageMultiplier, 1f) ||
                !Mathf.Approximately(EnemyDamageMultiplier, 1f) ||
                !Mathf.Approximately(SpawnRateMultiplier, 1f) ||
                !Mathf.Approximately(BossCountMultiplier, 1f);

            NoCrits = false;
            DenseFog = false;
            PlayerDamageMultiplier = 1f;
            EnemyDamageMultiplier = 1f;
            SpawnRateMultiplier = 1f;
            BossCountMultiplier = 1f;

            if (!silent && anyChange) OnEffectsChanged?.Invoke();
        }
    }
}
