using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Tutorial
{
    /// <summary>
    /// Drives the contextual tutorial / onboarding flow.
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// <list type="bullet">
    /// <item>Owns the list of <see cref="TutorialStep"/>s for the current run.</item>
    /// <item>Subscribes to gameplay events (state changes, kills, XP, level,
    /// damage) so it can decide when to trigger and when to dismiss steps.</item>
    /// <item>Polls the player's transform once per frame to detect "the player
    /// has moved at least X world units" for the movement step.</item>
    /// <item>Talks to a single <see cref="TipPopup"/> instance to display the
    /// currently active step.</item>
    /// </list>
    ///
    /// Persistence is delegated to <see cref="TutorialState"/>. Steps that have
    /// already been completed in a previous session are filtered out, except for
    /// transient "always-on" reminders like the low-HP hint.
    ///
    /// All Awake / Start work is null-safe: <see cref="PlayerController.Instance"/>
    /// can be null for the first frame after a scene load, and the manager
    /// gracefully retries from <see cref="Update"/>.
    /// </remarks>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("If left empty, the default first-run sequence is loaded at Awake.")]
        [SerializeField] private List<TutorialStep> steps = new();

        [Tooltip("Reference to the popup that actually renders tutorial content.")]
        [SerializeField] private TipPopup popup;

        [Header("Behaviour")]
        [Tooltip("World-space distance the player must travel to dismiss the movement step.")]
        [SerializeField] private float moveCompletionDistance = 2f;

        [Tooltip("HP fraction below which the OnLowHealth trigger fires.")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float lowHealthFraction = 0.3f;

        [Tooltip("Playtime (s) before the pause-button hint becomes available.")]
        [SerializeField] private float pauseHintAfterSeconds = 10f;

        [Tooltip("If true, this manager survives scene loads.")]
        [SerializeField] private bool persistAcrossScenes = true;

        /// <summary>Per-step bookkeeping kept separate from the inspector data.</summary>
        private class Runtime
        {
            public TutorialStep step;
            public bool triggered;       // trigger fired and queued/visible
            public bool shown;           // popup currently up
            public bool completed;       // dismissed + persisted
            public float showAt;         // unscaled time the popup should appear
            public float autoDismissAt;  // unscaled time popup auto-dismisses
            public float moveStartDist;  // accumulated movement distance baseline
        }

        private readonly List<Runtime> _runtime = new();
        private Runtime _active;          // currently visible step, if any

        private PlayerController _player;
        private PlayerHealth _health;
        private PlayerLevel _level;

        private Vector3 _lastPlayerPos;
        private float _movedDistance;
        private bool _movementBaselineSet;

        private bool _firstKillFired;
        private bool _firstXpFired;
        private bool _firstLevelUpFired;
        private bool _firstUpgradePickedFired;
        private bool _pauseHintFired;

        private bool _subscribedGame;
        private bool _subscribedPlayer;
        private bool _runStarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

            if (steps == null || steps.Count == 0)
            {
                steps = DefaultSteps();
            }

            BuildRuntime();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeGame();
            UnsubscribePlayer();
        }

        private void Start()
        {
            TrySubscribeGame();
            TrySubscribePlayer();

            // Bootstrap marker: as soon as the tutorial manager comes alive we
            // record that the player has experienced the system at least once.
            // IsFirstEverRun snapshots BEFORE we write this; downstream filtering
            // uses the cached _isFirstEverRun field captured during BuildRuntime.
            TutorialState.MarkCompleted(TutorialState.BootstrapStepId);
        }

        // ----- Default content -------------------------------------------------

        /// <summary>
        /// Returns the default first-run tutorial sequence. Keep step IDs stable
        /// — they are persisted in PlayerPrefs.
        /// </summary>
        public static List<TutorialStep> DefaultSteps()
        {
            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    id = "tut.move",
                    headline = "Tap and drag the stick to move.",
                    body = "Drag the on-screen joystick to walk around the arena.",
                    trigger = TutorialTrigger.OnRunStart,
                    completion = TutorialCondition.OnMoveDistance,
                    displayDelaySeconds = 0.5f,
                },
                new TutorialStep
                {
                    id = "tut.firstKill",
                    headline = "Your weapon fires automatically. Stay alive!",
                    body = "You don't need to aim. Just keep moving and dodging.",
                    trigger = TutorialTrigger.OnRunStart,
                    completion = TutorialCondition.OnFirstKill,
                    displayDelaySeconds = 5f,
                },
                new TutorialStep
                {
                    id = "tut.firstXp",
                    headline = "Walk over green gems to gain XP.",
                    body = "Pickups magnetise when you get close.",
                    trigger = TutorialTrigger.OnFirstKill,
                    completion = TutorialCondition.OnFirstXpPickup,
                    displayDelaySeconds = 0f,
                },
                new TutorialStep
                {
                    id = "tut.firstLevelUp",
                    headline = "Pick an upgrade!",
                    body = "Choose one of three perks to grow stronger.",
                    trigger = TutorialTrigger.OnFirstLevelUp,
                    completion = TutorialCondition.OnUpgradePicked,
                    displayDelaySeconds = 0f,
                },
                new TutorialStep
                {
                    id = "tut.pause",
                    headline = "Tap the pause button anytime.",
                    body = "You can rest, check stats, or quit from the pause menu.",
                    trigger = TutorialTrigger.OnPauseAvailable,
                    completion = TutorialCondition.OnUserDismiss,
                    displayDelaySeconds = 0f,
                },
                new TutorialStep
                {
                    id = "tut.lowHealth",
                    headline = "Heal pickups restore HP. Survive!",
                    body = "Stay near the edges of the swarm and grab any hearts that drop.",
                    trigger = TutorialTrigger.OnLowHealth,
                    completion = TutorialCondition.OnUserDismiss,
                    displayDelaySeconds = 0f,
                },
            };
        }

        // ----- Runtime construction -------------------------------------------

        private void BuildRuntime()
        {
            _runtime.Clear();
            // Snapshot IsFirstEverRun before we mark the bootstrap step in Start.
            bool firstRun = TutorialState.IsFirstEverRun;
            foreach (var step in steps)
            {
                if (step == null || string.IsNullOrEmpty(step.id)) continue;
                if (TutorialState.IsCompleted(step.id)) continue;
                // First-run-only steps are filtered out for returning players,
                // EXCEPT for the always-helpful low-HP nudge which we keep on
                // for everyone until it's been dismissed at least once.
                if (!firstRun && step.id != "tut.lowHealth") continue;

                _runtime.Add(new Runtime { step = step });
            }
        }

        // ----- Event wiring ---------------------------------------------------

        private void TrySubscribeGame()
        {
            if (_subscribedGame || GameManager.Instance == null) return;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            GameManager.Instance.OnKillsChanged += HandleKillsChanged;
            _subscribedGame = true;

            // If we entered Play state before the manager finished waking, fire
            // the run-start trigger now.
            if (GameManager.Instance.State == GameState.Playing && !_runStarted)
            {
                BeginRunTriggers();
            }
        }

        private void UnsubscribeGame()
        {
            if (!_subscribedGame || GameManager.Instance == null) return;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnKillsChanged -= HandleKillsChanged;
            _subscribedGame = false;
        }

        private void TrySubscribePlayer()
        {
            if (_subscribedPlayer) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;

            _player = pc;
            _health = pc.GetComponent<PlayerHealth>();
            _level = pc.GetComponent<PlayerLevel>();

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
            }
            if (_level != null)
            {
                _level.OnLevelUp += HandleLevelUp;
                _level.OnXpChanged += HandleXpChanged;
            }

            _lastPlayerPos = pc.transform.position;
            _movementBaselineSet = true;
            _movedDistance = 0f;
            _subscribedPlayer = true;
        }

        private void UnsubscribePlayer()
        {
            if (!_subscribedPlayer) return;
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            if (_level != null)
            {
                _level.OnLevelUp -= HandleLevelUp;
                _level.OnXpChanged -= HandleXpChanged;
            }
            _subscribedPlayer = false;
        }

        // ----- Game event handlers --------------------------------------------

        private void HandleStateChanged(GameState prev, GameState next)
        {
            if (next == GameState.Playing && !_runStarted)
            {
                BeginRunTriggers();
            }

            // Upgrade-picked detection: the level-up modal sets state to
            // LevelUp on level up, and back to Playing once the choice is made.
            if (prev == GameState.LevelUp && next == GameState.Playing)
            {
                _firstUpgradePickedFired = true;
                DismissByCondition(TutorialCondition.OnUpgradePicked);
            }

            if (next == GameState.GameOver || next == GameState.Victory)
            {
                if (_active != null) popup?.Hide();
            }
        }

        private void HandleKillsChanged(int kills)
        {
            if (kills > 0 && !_firstKillFired)
            {
                _firstKillFired = true;
                TriggerByEvent(TutorialTrigger.OnFirstKill);
                DismissByCondition(TutorialCondition.OnFirstKill);
            }
        }

        private void HandleXpChanged(int current, int toNext)
        {
            // Any XP changed event with current > 0 (or any non-zero delta)
            // implies a gem pickup. We can't distinguish gem pickups from
            // upgrade-granted XP perfectly, but treating the first XpChanged
            // post-startup with current > 0 as the first pickup is a fine
            // approximation for onboarding.
            if (!_firstXpFired && current > 0)
            {
                _firstXpFired = true;
                TriggerByEvent(TutorialTrigger.OnFirstXpPickup);
                DismissByCondition(TutorialCondition.OnFirstXpPickup);
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            if (!_firstLevelUpFired)
            {
                _firstLevelUpFired = true;
                TriggerByEvent(TutorialTrigger.OnFirstLevelUp);
                DismissByCondition(TutorialCondition.OnFirstLevelUp);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (max <= 0f) return;
            float fraction = current / max;
            if (fraction <= lowHealthFraction)
            {
                TriggerByEvent(TutorialTrigger.OnLowHealth);
            }
        }

        private void BeginRunTriggers()
        {
            _runStarted = true;
            TriggerByEvent(TutorialTrigger.OnRunStart);
        }

        // ----- Trigger / dismiss bookkeeping ----------------------------------

        private void TriggerByEvent(TutorialTrigger trigger)
        {
            float now = Time.unscaledTime;
            foreach (var rt in _runtime)
            {
                if (rt.completed || rt.triggered) continue;
                if (rt.step.trigger != trigger) continue;
                rt.triggered = true;
                rt.showAt = now + Mathf.Max(0f, rt.step.displayDelaySeconds);
            }
        }

        private void DismissByCondition(TutorialCondition condition)
        {
            if (_active == null) return;
            if (_active.step.completion != condition) return;
            CompleteActive();
        }

        private void CompleteActive()
        {
            if (_active == null) return;
            var rt = _active;
            rt.completed = true;
            rt.shown = false;
            TutorialState.MarkCompleted(rt.step.id);
            popup?.Hide();
            _active = null;
        }

        // ----- Main loop ------------------------------------------------------

        private void Update()
        {
            // Late subscription: handles the case where TutorialManager woke
            // before GameManager / PlayerController existed.
            if (!_subscribedGame) TrySubscribeGame();
            if (!_subscribedPlayer) TrySubscribePlayer();

            // Pause-hint trigger: fires once the player has been in a Playing
            // state long enough to mean they're actually engaged with the game.
            if (!_pauseHintFired && GameManager.Instance != null &&
                GameManager.Instance.ElapsedSeconds >= pauseHintAfterSeconds)
            {
                _pauseHintFired = true;
                TriggerByEvent(TutorialTrigger.OnPauseAvailable);
            }

            UpdateMovement();
            UpdateActivePopup();
            PromoteNextStep();
        }

        private void UpdateMovement()
        {
            if (_player == null) return;
            if (!_movementBaselineSet)
            {
                _lastPlayerPos = _player.transform.position;
                _movementBaselineSet = true;
                return;
            }
            var pos = _player.transform.position;
            _movedDistance += Vector3.Distance(pos, _lastPlayerPos);
            _lastPlayerPos = pos;

            // Dismiss the move step if it's the one currently visible and the
            // player has accumulated enough distance from when we last reset
            // the baseline.
            if (_active != null && _active.step.completion == TutorialCondition.OnMoveDistance)
            {
                if (_movedDistance - _active.moveStartDist >= moveCompletionDistance)
                {
                    CompleteActive();
                }
            }
        }

        private void UpdateActivePopup()
        {
            if (_active == null) return;

            // Timeout-based dismissal works against unscaled time so popups
            // shown during pause / level-up still expire.
            if (_active.step.completion == TutorialCondition.OnTimeout)
            {
                if (Time.unscaledTime >= _active.autoDismissAt)
                {
                    CompleteActive();
                }
            }

            // Soft cap: even non-timeout popups disappear after a generous
            // ceiling so a forgotten step doesn't sit forever on screen.
            // (Skipped for OnUpgradePicked which is gated on a modal choice.)
        }

        private void PromoteNextStep()
        {
            if (_active != null) return;

            float now = Time.unscaledTime;
            foreach (var rt in _runtime)
            {
                if (rt.completed || rt.shown) continue;
                if (!rt.triggered) continue;
                if (now < rt.showAt) continue;

                ShowStep(rt);
                return;
            }
        }

        private void ShowStep(Runtime rt)
        {
            _active = rt;
            rt.shown = true;
            rt.moveStartDist = _movedDistance;

            // Configure timeout defaults if the step uses one.
            if (rt.step.completion == TutorialCondition.OnTimeout)
            {
                // pause / lowHealth come with explicit per-step expectations;
                // fall back to a generous 8s default otherwise.
                float timeout = rt.step.id switch
                {
                    "tut.pause" => 8f,
                    "tut.lowHealth" => 6f,
                    "tut.firstLevelUp" => 30f,
                    _ => 8f,
                };
                rt.autoDismissAt = Time.unscaledTime + timeout;
            }

            popup?.Show(rt.step);

            // Best-effort audio cue. AudioManager.PlaySfx wants an AudioClip,
            // but Agent 4's named-cue lookup isn't merged yet — we only resolve
            // through reflection if it's available, otherwise silently skip.
            TryPlayCue(rt.step.cue);
        }

        private void TryPlayCue(AudioCueRef cue)
        {
            if (string.IsNullOrEmpty(cue.cueName)) return;
            var am = AudioManager.Instance;
            if (am == null) return;
            // Look for a method `PlayCue(string)` so we can hook into whatever
            // Agent 4 ships without taking a compile-time dependency on it.
            var method = am.GetType().GetMethod("PlayCue", new[] { typeof(string) });
            if (method != null)
            {
                method.Invoke(am, new object[] { cue.cueName });
            }
            // Otherwise no-op: a named lookup isn't available yet.
        }

        // ----- Public API -----------------------------------------------------

        /// <summary>
        /// Force a step to be triggered now, bypassing its normal trigger.
        /// Useful for debug menus or designer-driven hints.
        /// </summary>
        public void TriggerManually(string stepId)
        {
            foreach (var rt in _runtime)
            {
                if (rt.completed || rt.triggered) continue;
                if (rt.step.id != stepId) continue;
                rt.triggered = true;
                rt.showAt = Time.unscaledTime + Mathf.Max(0f, rt.step.displayDelaySeconds);
                return;
            }
        }

        /// <summary>
        /// User tapped the popup. Dismisses the active step IF its completion
        /// condition allows tap-to-dismiss.
        /// </summary>
        public void UserDismissActive()
        {
            if (_active == null) return;
            var cond = _active.step.completion;
            if (cond == TutorialCondition.OnUserDismiss || cond == TutorialCondition.OnTimeout)
            {
                CompleteActive();
            }
        }
    }
}
