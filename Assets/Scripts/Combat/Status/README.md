# Status Effect / DoT Framework

`namespace LoneFighter.Combat.Status`

Per-victim, tick-based status effects (DoT, slow, stun, ...). Designed to be driven from the
sibling Elements track at `Assets/Scripts/Combat/Elements/`, but is fully decoupled — anything
holding a reference to a `StatusController` can apply effects.

## Files

| File                     | Role                                                                |
|--------------------------|---------------------------------------------------------------------|
| `StatusEffect.cs`        | Abstract base. Holds duration / tickInterval / Id / lifecycle hooks |
| `StatusController.cs`    | MonoBehaviour. Owns the active-effect list; ticks on FixedUpdate    |
| `BurnEffect.cs`          | 5 dmg / 0.5s for 3s. Refresh-only stacking                          |
| `SlowEffect.cs`          | x0.4 move-speed for 2s. Refresh-only stacking                       |
| `SlowSpeedRegistry.cs`   | Helper: caches original move-speed per `EnemyChaseAI`               |
| `StunEffect.cs`          | Halts movement for 1s. Disables AI + zeroes velocity each tick      |
| `PoisonEffect.cs`        | 2 dmg / 0.5s per stack, 5s, up to 5 stacks. Stacks on re-apply      |
| `StatusIcons.cs`         | World-space TMP icon row above the victim. Pooled                   |
| `StatusVisualHooks.cs`   | Spawns / despawns a particle prefab per effect type                 |

## Usage

```csharp
// On any damageable (enemy / player), make sure StatusController is attached.
// You'll typically do this on the enemy prefab next to EnemyHealth.

// Apply from a weapon / element hit:
var sc = victim.GetComponent<StatusController>();
if (sc != null) sc.Apply(new BurnEffect());

// Query:
if (sc.Has(BurnEffect.EffectId)) { ... }

// Cleanup (e.g. on enemy death before returning to pool):
sc.RemoveAll();
```

## Stacking semantics

The base class implements **refresh-only** stacking in `OnReapply()`: re-applying an effect
extends its expiry to `now + incoming.duration` if that's later than the current expiry, and
otherwise does nothing.

`PoisonEffect` overrides `OnReapply()` to **also** add a stack (clamped to `maxStacks`), so
re-application both refreshes and stacks.

`BurnEffect` and `StunEffect` keep the refresh-only base behavior explicitly.

## Slow + EnemyChaseAI

`EnemyChaseAI.moveSpeed` is private serialized and has no public getter. We are not allowed
to modify `EnemyChaseAI`, so `SlowSpeedRegistry` recovers the original value by serializing
the component via `JsonUtility.ToJson` and parsing the `"moveSpeed":<num>` token. It caches
the result per AI-instance until the slow ends, then forgets it. This is reflection-free
(JsonUtility is Unity's built-in serializer) and IL2CPP-friendly.

If the JSON snapshot fails (e.g. someone renames the field), the registry defaults to `2f`
and emits a warning via `Debug.LogWarning`. Replace the registry with a clean getter as soon
as `EnemyChaseAI` exposes one.

## Visuals

- `StatusIcons` is **self-bootstrapping**: drop it next to a `StatusController` and it builds
  its own world-space canvas + procedural TMP icon row on `Awake`. Override with pre-authored
  references in the inspector if you want a custom look.
- `StatusVisualHooks` is **data-driven**: assign one prefab per effect Id (`"burn"`, `"slow"`,
  `"stun"`, `"poison"`) in the inspector. It parents the prefab to the victim while the effect
  is live and releases it back to `PoolService` when the effect ends.
- `StunEffect` also triggers a `FxService.LightShake()` on apply when the FX service is present.

## Tick model

`StatusController.FixedUpdate` walks the active list, calls `OnTick` whenever
`Time.time >= effect.NextTickTime`, then re-schedules. `tickInterval == 0` means "tick every
FixedUpdate" — used by `StunEffect` to win the race with `EnemyChaseAI`'s velocity writes
(StatusController runs after EnemyChaseAI alphabetically in default script order).

Expiry is checked in the same pass; expired effects fire `OnRemove` and `OnEffectRemoved`.

## Integration checklist

1. Add `StatusController` to the enemy prefab (next to `EnemyHealth`) and to the player.
2. (Optional) Add `StatusIcons` and `StatusVisualHooks` siblings for visual feedback.
3. From your hit handler, call `victim.GetComponent<StatusController>()?.Apply(new BurnEffect())`.
4. On enemy death / pool-return, the controller auto-clears via `OnDisable`.
