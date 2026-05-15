# Kill streaks (`LoneFighter.Combat.Streaks`)

Long-running, **uninterrupted** kill-streak system. Distinct from
`LoneFighter.Polish.ComboCounter`, which uses a 3-second sliding window — this
system measures *survival under pressure*: every kill adds 1 to the streak, and
the streak resets the instant the player takes any damage.

Nothing outside this folder is modified. All wiring is done via subscriptions
to existing public events on `GameManager` and `PlayerHealth`.

## Files

| File | Role |
| --- | --- |
| `KillStreakTracker.cs` | Singleton MonoBehaviour. Owns `Current`, `BestThisRun`, `OnStreakChanged`, `OnStreakBroken`. |
| `KillStreakRewards.cs` | Tiered rewards at {10, 25, 50, 100, 250}: bonus XP, damage buff, free reroll, banner. Exposes `OnStreakBuff`, `OnRerollGranted`, `OnRewardTierReached` static events. |
| `StreakBanner.cs` | Slide-in screen banner ("10 KILL STREAK!", "25!", …). Escalates font size + color per tier. Unscaled time. Auto-hides after `holdSeconds` (default 1.8s). |
| `StreakChip.cs` | Small persistent HUD chip (top-center, below the timer). Pulses on each new kill. |
| `StreakUpgradeAdapter.cs` | Placeholder for a future **Streaker** upgrade ("+XP bonus per kill in streak"). Documented hooks only — no live effect until enabled. |

## Authoring (one-time, in the Unity editor)

1. Create an empty GameObject `Systems_Streaks` in the **Game** scene.
2. Add the following components in this order:
   - `KillStreakTracker`
   - `KillStreakRewards`
   - `StreakUpgradeAdapter` *(harmless to add early; does nothing without the upgrade)*
3. **HUD chip**:
   - Under the HUD canvas, create a small RectTransform anchored to top-center,
     positioned just below the run timer (~60×40 px).
   - Add a child `TMP_Text` (size ~24, bold).
   - Add `StreakChip` to the chip root and wire the `TMP_Text` into the `label`
     slot. Optional: add a `CanvasGroup` for cleaner fading.
4. **Streak banner**:
   - Under the HUD canvas, create a wide RectTransform anchored to center-right
     (~600×140 px).
   - Add a child `TMP_Text` (auto-size off, big — see tier sizes in
     `StreakBanner.fontSizes`).
   - Add `StreakBanner` and a `CanvasGroup`; wire them up.
   - Drop the banner into the `banner` slot on `KillStreakRewards`, or leave it
     null — `KillStreakRewards` auto-discovers a single scene-level banner on
     `Start`.

## Runtime behavior

- **Increment**: `GameManager.RegisterKill()` fires `OnKillsChanged(totalKills)`.
  `KillStreakTracker` translates the delta into per-kill `Current++` increments
  and fires `OnStreakChanged(Current)` for each one.
- **Reset**: `PlayerHealth.OnHealthChanged(current, max)` is observed; if
  `current` decreased, the streak resets to 0 and `OnStreakBroken(previousStreak)`
  fires. Healing or `SetMax(refill: true)` does **not** break the streak (only
  decreases in `Current` count).
- **Run reset**: `GameManager.BeginRun()` zeros `Kills` and re-fires
  `OnKillsChanged(0)`. The tracker interprets a non-positive delta as a run
  reset and silently clears `Current` and `BestThisRun`.

## Reward tiers

| Streak | Bonus XP | Damage buff | Free reroll | Banner text |
| ---: | ---: | ---: | :---: | :--- |
| 10 | 5 | +10% × 8s | — | `10 KILL STREAK!` |
| 25 | 15 | +20% × 10s | YES | `25!` |
| 50 | 40 | +35% × 12s | YES | `50!` |
| 100 | 100 | +60% × 15s | YES | `100!` |
| 250 | 300 | +100% × 20s | YES | `250!` |

Each tier fires **once per streak**. Breaking the streak resets the awarded-tier
bookkeeping so the next streak earns its tiers again.

## Public surface

```csharp
// State.
KillStreakTracker.Instance.Current;       // int
KillStreakTracker.Instance.BestThisRun;   // int

// Per-streak events.
KillStreakTracker.Instance.OnStreakChanged += (cur) => { ... };
KillStreakTracker.Instance.OnStreakBroken  += (lost) => { ... };

// Reward events (static — survive scene reloads).
KillStreakRewards.OnStreakBuff       += (StreakBuff b) => { ... };
KillStreakRewards.OnRerollGranted    += (tier) => { ... };
KillStreakRewards.OnRewardTierReached += (tier) => { ... };
```

### `StreakBuff` payload

```csharp
public readonly struct StreakBuff
{
    public readonly int Tier;
    public readonly float DamageMultiplier;  // e.g. 1.10f
    public readonly float DurationSeconds;   // unscaled
}
```

Consumers (e.g. weapon damage calculators) subscribe, multiply their damage
output by `b.DamageMultiplier` for `b.DurationSeconds` of unscaled time, and
stack multiplicatively with any other buffs. None of that is implemented yet —
the event is fired so the wiring is forward-compatible.

### `OnRerollGranted`

Today, `KillStreakRewards` only **fires the event** and logs to console. To
make the reroll actually free, the upgrade picker UI should subscribe and
display a "Free Reroll" button while a counter is > 0. That counter / UI work
lives outside this folder.

## Future upgrade: **Streaker** ("+XP bonus per kill in streak")

`StreakUpgradeAdapter` is the documented integration point. The plan:

1. Author a new `Streaker.asset` (UpgradeData), with `OnApply` calling
   `StreakUpgradeAdapter.Instance.StackUp()` on each pick. (Requires editing
   `UpgradeService.cs` or `UpgradeData.cs`, **out of scope** for this folder.)
2. Set `enableBonusXp = true` on the adapter in the inspector.
3. The adapter listens to `OnStreakChanged`, computes
   `xpPerKillPerStack * Stacks * min(streak, maxScalingStreak)` and grants it
   via `PlayerLevel.AddXp` for each kill in the streak.

The math, stack ceiling, and UI hook (`OnBonusXpGranted`) are already wired —
all that's left when the upgrade ships is the asset and the `UpgradeService`
plumbing.

## Why two streak systems?

`ComboCounter` (existing) rewards **bursty** play — clearing a swarm in 3s.
`KillStreakTracker` (this folder) rewards **clean** play — never getting hit.
They complement each other and intentionally use different timelines, different
HUD slots, and different reward tables. There is no shared state; each
subscribes independently to `GameManager.OnKillsChanged`.
