# HapticsExtras

Rich, multi-pulse haptic feedback for **LoneFighter**, layered on top of the existing
`HapticsService` / `HapticsBridge` without modifying them.

Namespace: `LoneFighter.Polish.HapticsExtras`.

## What's here

| File | Role |
| --- | --- |
| `HapticPattern.cs` | Ordered sequence of (intensity, duration, gapMs) steps. `IEnumerator Play(HapticsService, scalar)` coroutine. |
| `HapticPatternLibrary.cs` | Static catalog of named patterns: `DoublePulse`, `TriplePulse`, `RisingRumble`, `Explosion`, `BossArrival`, `Victory`, `Defeat`, `LevelUpFlourish`, `DashTick`. |
| `HapticDirector.cs` | Singleton MonoBehaviour. Subscribes to gameplay events and dispatches patterns through `HapticsService.Instance`, with rate-limiting and an intensity curve. |
| `WeaponHapticBindings.cs` | Per-weapon hit feedback. Detects weapon fires (events if available, else polling `_cooldownTimer`) and pulses Light. |
| `HapticIntensityCurve.cs` | Scales pattern strength by gameplay chaos. Reads an optional sibling `IntensityResolver` via reflection — no compile-time dependency. |
| `HapticsSettings.cs` | PlayerPrefs accessor for haptic-strength multiplier (0..2). Separate from `GameSettings.HapticsEnabled`. |

## How to wire it up (Editor)

In `Game.unity`, on the `Systems` parent (or anywhere persistent):

1. Add a child GameObject `HapticDirector` → add the `HapticDirector` component.
2. Optionally add a child GameObject `WeaponHapticBindings` → add the component. Leave
   `Inventory` blank to auto-find the player's `WeaponInventory` at runtime.

The existing `HapticsBridge` can stay or be removed — both can coexist.

## Event → Pattern map

| Source | Trigger | Pattern |
| --- | --- | --- |
| `PlayerLevel.OnLevelUp` | Any level up | `LevelUpFlourish` |
| `GameManager.OnStateChanged` | → `Victory` | `Victory` |
| `GameManager.OnStateChanged` | → `GameOver` | `Defeat` |
| `PlayerHealth.OnHealthChanged` | Single hit ≥ 20% max HP | `Explosion` |
| `EnemyRegistry` poll | New enemy whose `displayName` matches `boss` / `warden` / `elite` | `BossArrival` |
| `GameManager.OnKillsChanged` | Every Nth kill (default N=10) | `TriplePulse` |
| `ComboCounter.OnComboChanged` | Crosses threshold (default 10) | `RisingRumble` |
| `WeaponBase` fired | Each shot (rate-limited) | Single `Light` pulse |

All thresholds and rate-limits are inspector knobs on `HapticDirector` and
`WeaponHapticBindings`.

## Intensity curve (optional)

If your project has an `IntensityResolver` (commonly part of adaptive audio systems —
`LoneFighter.Audio.Adaptive.IntensityResolver` is one example), `HapticIntensityCurve`
will find it by reflection and use its `Current` / `GetCurrent()` reading to scale
pattern strength. Higher gameplay chaos → punchier rumble. With no resolver present, the
curve falls back to a neutral 0.5 and the multiplier is ~0.85, which "plays as authored."

## User strength dial

`HapticsSettings.Strength` is a 0..2 PlayerPrefs-backed multiplier:

| Value | Behavior |
| --- | --- |
| 0.0 | Effectively muted (director short-circuits before any pulse). |
| 1.0 | Default. Plays as authored. |
| 2.0 | Maximum — keeps everything at its top intensity even when the curve would dial it back. |

`GameSettings.HapticsEnabled` is still the master on/off and is honored by both
`HapticDirector` and `WeaponHapticBindings`.

## Calling patterns from gameplay code

For ad-hoc triggers (e.g. dash, pickup), call:

```csharp
HapticDirector.Instance?.Play(HapticPatternLibrary.DashTick);
// or
HapticDirector.Instance?.PlayByName("DoublePulse");
```

Going through the director (rather than `HapticsService` directly) gets you:

- Master on/off gate (`GameSettings.HapticsEnabled`)
- Strength multiplier (`HapticsSettings.Strength`)
- Intensity curve scaling
- Global cooldown to prevent spam
- One-pattern-at-a-time policy (a dramatic event preempts a mundane one)

## Mobile notes

The underlying `HapticsService` skips `Light` on Android (battery-friendly) and uses
`Handheld.Vibrate()` for Medium/Heavy. iOS uses the same fallback unless a CoreHaptics
plugin is added. Gamepads (if connected) get the full pattern via rumble motors. Pattern
"durations" are honored as silent holds between steps so the cadence is correct even on
platforms where the OS picks the pulse length.
