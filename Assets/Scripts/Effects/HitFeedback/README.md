# Hit Feedback

Layered, stacking "the player should FEEL it" feedback. Every hit fans out
across multiple channels simultaneously so combat reads as visceral instead of
abstract. All scripts in the `LoneFighter.Effects.HitFeedback` namespace.

Designed as a **drop-in additive layer**: no existing project file is modified.
The system wires itself up at runtime by:
- Polling `EnemyRegistry.Enemies` for newly-spawned enemies and attaching the
  four per-enemy feedback components automatically.
- Polling `EnemyHealth.Current` for HP drops to detect hits without needing a
  hook into the project's damage pipeline.
- Looking up the scene's URP `Volume` for vignette / chromatic / lens
  distortion overrides, and gracefully no-op'ing if one isn't present.

## File map

| File | Type | Role |
| --- | --- | --- |
| `HitFeedbackService.cs` | MonoBehaviour (singleton) | Central facade. `OnEnemyHit(EnemyBase, damage, hitDirection)` and `OnPlayerHit(damage, hitDirection)` dispatch to every available channel. |
| `EnemySpriteFlash.cs` | MonoBehaviour | Flashes all child SpriteRenderers pure white for 0.06s on hit; restores originals. |
| `EnemyKnockback.cs` | MonoBehaviour | Sets Rigidbody2D linearVelocity along hit direction for 0.1s; suspends `EnemyChaseAI` during the kick so it isn't overwritten. |
| `EnemyHitPause.cs` | MonoBehaviour | Disables `EnemyChaseAI` for 0.04s for a tiny "stagger"; skips named boss components by default (`WardenBoss`). |
| `EnemyFeedbackAttacher.cs` | MonoBehaviour (singleton) | Polls `EnemyRegistry` and adds the four feedback components + `HitListener` to every new enemy. |
| `HitListener.cs` | MonoBehaviour | Polls `EnemyHealth.Current` each frame and emits hits to `HitFeedbackService` on HP drop. |
| `PlayerHitVignette.cs` | MonoBehaviour | Red URP Vignette pulse on every player damage event. Gracefully no-ops if Volume is missing. |
| `PlayerHitFreezeFrame.cs` | MonoBehaviour | 0.08s freeze on hits removing >20% of player max HP. Routes through `FxService.HitStop` when available. |
| `BloodSplashOverlay.cs` | MonoBehaviour | Edge-anchored red splatter Images on a child Canvas; ramp up below 25% HP, denser as HP drops. |
| `ImpactRipple.cs` | MonoBehaviour | Pool of small additive sprite shockwaves spawned at hit positions. Builds a fallback sprite if none provided. |
| `LowHpScreenWarp.cs` | MonoBehaviour | Chromatic aberration + lens distortion below 25% HP, breathing with a slow sine. |
| `HitDamageNumbers.cs` | MonoBehaviour | Self-pooled `TextMesh` popups. Shake amplitude scales with damage; above the big-hit threshold a "BIG HIT!" tag spawns above. |

## Scene setup

The fastest way to get the full kit running:

1. Add an empty `_HitFeedback` GameObject to your gameplay scene.
2. Attach:
   - `HitFeedbackService`
   - `EnemyFeedbackAttacher`
   - `PlayerHitVignette`
   - `PlayerHitFreezeFrame`
   - `BloodSplashOverlay`
   - `LowHpScreenWarp`
   - `HitDamageNumbers`
   - `ImpactRipple`
3. Make sure the scene has a URP Global Volume with **Vignette**,
   **ChromaticAberration**, and **LensDistortion** overrides on its profile.
   (Any subset works — missing overrides are skipped without errors.)

That's it. Enemy-side components attach themselves to every spawned enemy via
`EnemyFeedbackAttacher`, and `HitListener` detects damage by polling
`EnemyHealth`. The player-side components subscribe to
`PlayerHealth.OnHealthChanged` directly.

## How damage flows

- **Enemy side (automatic):** `EnemyFeedbackAttacher` adds `EnemySpriteFlash`,
  `EnemyKnockback`, `EnemyHitPause`, and `HitListener` to every enemy at spawn.
  When the enemy's HP drops, `HitListener` calls
  `HitFeedbackService.OnEnemyHit(...)`, which fires all four enemy components +
  spawns an `ImpactRipple` + a `HitDamageNumbers` popup.
- **Player side (mostly automatic):** `PlayerHitVignette`,
  `PlayerHitFreezeFrame`, `BloodSplashOverlay`, and `LowHpScreenWarp` all
  subscribe to `PlayerHealth.OnHealthChanged` and react autonomously. If you'd
  like to additionally route through the facade — for example because you want
  the "BIG HIT!" tag to show on the player too — call
  `HitFeedbackService.Instance.OnPlayerHit(damage, hitDirection)` from your
  damage code. (Optional. The facade is safe to skip; nothing in this folder
  requires modifications to existing scripts.)

## Tuning

Each component exposes inspector knobs for thresholds, intensities, durations,
and colors. The defaults match the spec:

- `EnemySpriteFlash`: 0.06s flash, pure white.
- `EnemyKnockback`: 6 m/s impulse, 0.1s window.
- `EnemyHitPause`: 0.04s AI suspension, boss-skipping enabled.
- `PlayerHitFreezeFrame`: triggers above 20% HP-lost-in-one-event, 0.08s
  freeze.
- `BloodSplashOverlay`: ramps up below 25% HP.
- `LowHpScreenWarp`: ramps up below 25% HP.
- `HitDamageNumbers`: shake scales linearly to 0.25 world units of jitter at
  60+ damage; "BIG HIT!" tag at the same threshold.

All thresholds are inspector-overrideable so balance can change without code
edits.

## Boss handling

`EnemyHitPause` deliberately skips bosses (anything with a `WardenBoss` named
component on the same GameObject — list extensible from inspector). The other
channels still fire — bosses still flash, get knocked back (slightly), and
show damage numbers. Only the AI-stagger is suppressed, since bosses have
choreographed phases that a stagger would visually fight with.

## URP requirements

Optional but recommended. Without a URP Volume the vignette, chromatic
aberration, and lens distortion effects no-op cleanly. The rest of the system
(flashes, knockback, hit pause, ripple, popups, blood overlay) is renderer-
agnostic and works in any pipeline.

## Performance notes

- Per-enemy components do constant-time work. The flash uses a cached
  `SpriteRenderer[]`; the knockback only fires its impulse once per hit; the
  hit-pause toggles a single Behaviour flag.
- `HitListener` does one float compare per enemy per frame, well below any
  perf budget on mobile.
- `EnemyFeedbackAttacher` polls `EnemyRegistry` at 10 Hz by default and uses a
  HashSet to skip already-wired enemies — O(new-enemies) per sweep.
- `ImpactRipple` and `HitDamageNumbers` pool their visuals in-house, so
  per-hit allocation is zero after warm-up.
- `BloodSplashOverlay` is four UI Images on a screen-space-overlay canvas —
  effectively free on the GPU.
- `PlayerHitVignette` and `LowHpScreenWarp` mutate URP override values
  directly. No GameObject churn.
