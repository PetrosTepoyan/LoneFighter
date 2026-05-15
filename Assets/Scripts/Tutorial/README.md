# Tutorial / Onboarding

Light-touch contextual onboarding for first-time LoneFighter players.

A new player drops into the arena and sees a sequence of small popups
("tips") nudging them through the core verbs — move, kill, collect XP,
pick an upgrade, pause — without taking control away from them. Tips
are gated on actual gameplay events: the move tip dismisses as soon as
the player has walked 2 units, the kill tip dismisses on their first
kill, and so on.

Tutorial progress is **persisted in PlayerPrefs** under
`LoneFighter.Tutorial.CompletedSteps`. Returning players skip everything
except the low-HP safety reminder, which we keep on for everyone.

## Pipeline

```
[Game events]                                 [Manager]                  [UI]
GameManager.OnStateChanged ──┐
GameManager.OnKillsChanged   ├──► TutorialManager ──► TipPopup.Show / Hide
PlayerLevel.OnLevelUp        │       │
PlayerLevel.OnXpChanged      │       │ marks step completed
PlayerHealth.OnHealthChanged │       ▼
PlayerController pos sample ─┘    TutorialState (PlayerPrefs)
```

* `TutorialState` is a static facade over PlayerPrefs. It exposes
  `IsCompleted(id)`, `MarkCompleted(id)`, `ResetAll()`, and
  `IsFirstEverRun`.
* `TutorialStep` is the inspector-editable data describing one popup:
  id, headline, body, trigger, completion condition, delay, optional
  icon, optional audio cue name.
* `TutorialManager` (singleton MonoBehaviour) owns the live list, wires
  itself to game events, polls the player's transform for movement, and
  drives `TipPopup`. It null-checks `PlayerController.Instance` because
  the player may not exist on the first frame after a scene load.
* `TipPopup` is the HUD UI component — a `CanvasGroup`-backed panel
  anchored top center, just below the timer / HP bar. It fades and
  scale-bounces using **unscaled** time so popups still animate during
  pause / level-up.

## Default steps

| id                 | trigger             | dismisses on          | notes |
| ------------------ | ------------------- | --------------------- | ----- |
| `tut.move`         | OnRunStart          | OnMoveDistance (2 u)  | First thing the player sees. |
| `tut.firstKill`    | OnRunStart (delay 5s) | OnFirstKill         | Reminds them weapons auto-fire. |
| `tut.firstXp`      | OnFirstKill         | OnFirstXpPickup       | Explains the gem. |
| `tut.firstLevelUp` | OnFirstLevelUp      | OnUpgradePicked       | Points at the upgrade modal. |
| `tut.pause`        | OnPauseAvailable (after 10s playtime) | OnUserDismiss / OnTimeout 8s | Soft hint. |
| `tut.lowHealth`    | OnLowHealth (<30% HP) | OnUserDismiss / OnTimeout 6s | Shown to returning players too. |

All steps EXCEPT `tut.lowHealth` are filtered out for returning players.

## Adding a new step

You have two options.

### Inspector authoring

The `TutorialManager` has a serialized `steps` list. If you populate it,
the default list is **not** auto-loaded. Add a new entry, set its `id`
to something unique and stable (it will be persisted as the PlayerPrefs
completion key), and choose the trigger / completion combination.

### Code (extend the defaults)

Edit `TutorialManager.DefaultSteps()` and add a new `TutorialStep`. If
your step needs a brand-new kind of trigger or completion, add an enum
value to `TutorialStep.cs` and wire the corresponding event in
`TutorialManager`.

When you add a new completion condition:
1. Add the enum case to `TutorialCondition`.
2. Find the gameplay event that should dismiss it. Subscribe to that
   event in `TutorialManager.TrySubscribeGame()` or
   `TrySubscribePlayer()`.
3. Call `DismissByCondition(TutorialCondition.YourNewCase)` from the
   handler.

When you add a new trigger:
1. Add the enum case to `TutorialTrigger`.
2. Detect the firing condition somewhere in `Update` or an event
   handler and call `TriggerByEvent(TutorialTrigger.YourNewCase)`.

## Resetting for testing

Two equivalent options:

* **Editor menu** — `LoneFighter → Tutorial → Reset Tutorial`. Wipes
  all completed-step state including the bootstrap marker, so the next
  run is treated as a first-ever launch. The companion menu
  `LoneFighter → Tutorial → Mark All Completed` fast-forwards past
  onboarding entirely.
* **Code** — `LoneFighter.Tutorial.TutorialState.ResetAll();`

The editor window at `LoneFighter → Tutorial → Open Flow Window`
lists every default step with per-step "Mark Done" / "Re-arm" buttons
for granular tweaking during QA.

## Notes

* Animations and timeouts run on `Time.unscaledTime` so popups continue
  to appear, animate, and expire during pause and level-up modals.
* Audio cues are referenced by **name string** (`AudioCueRef.cueName`)
  rather than by enum or asset reference because the audio system
  (Agent 4) was still in flight when this module landed. At runtime,
  `TutorialManager` looks for a `PlayCue(string)` method on
  `AudioManager` via reflection and silently no-ops if it isn't there
  yet.
* `TipPopup` should remain a single instance referenced by the
  `TutorialManager`'s `popup` field. Place it under the persistent HUD
  canvas, anchored top-center, below the timer / HP bar.
