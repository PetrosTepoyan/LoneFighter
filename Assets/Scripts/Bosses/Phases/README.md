# Boss Phases (`LoneFighter.Bosses`)

Multi-phase boss encounter framework. All files here are **new** — none of the existing
gameplay scripts in `Assets/Scripts/Enemies/...` are modified. Hooks into the existing
`EnemyHealth.OnDied` event and reads `Current` / `Max` via `TryGetComponent`.

## File map

| File | Role |
| --- | --- |
| `BossEncounter.cs` | MonoBehaviour on the boss prefab. Owns a `PhaseManager`, subscribes to `EnemyHealth.OnDied`, invokes `BossDeathSequence`. |
| `BossPhase.cs` | Abstract base — `Name`, `EntryHpFraction`, `Enter/Exit/Tick(ctx)`. |
| `PhaseManager.cs` | Sorted list of phases. Polls HP and advances on threshold crossings; handles multi-step transitions in a single frame. |
| `WardenPhases.cs` | Three concrete phases for the existing `WardenBoss` mini-boss (Default → Double Slam → Enrage). |
| `PhaseShockwaveDriver.cs` | Companion `MonoBehaviour` added at runtime when a phase needs to emit shockwaves on its own cadence. |
| `BossDeathSequence.cs` | Slow-mo (0.3× for 1.5s, unscaled), 5 shake/explosion bursts, full-screen white flash, optional guaranteed pickup drop. |
| `BossArenaSetup.cs` | Optional: shrinks `ArenaBounds.size` during the fight and restores on death. |

## Phase thresholds

`PhaseManager` sorts by `EntryHpFraction` descending:

| Phase | Entry HP | Behavior |
| --- | --- | --- |
| 1 — Default | 1.00 | Default chase, slam interval `(3.5, 4.5)`s |
| 2 — Double Slam | 0.66 | Slam interval `(2.6, 3.4)`s, each slam echoed by a second shockwave 0.35s later |
| 3 — Enrage | 0.33 | Continuous shockwaves every 1.5s, **+30% move speed**, plus the boss's natural slam cadence on top |

## Integration limit & workaround

The existing `WardenBoss` exposes its tuning fields as `private [SerializeField]` with **no**
public setters: `slamInterval`, `shockwavePrefab`, `_state`. Likewise `EnemyChaseAI.moveSpeed`
is private (but the file ships a public `Configure(float)`). Because we cannot modify those
files, the phase system does two things:

1. **Reflection (preferred)** — `WardenBossReflection` / `WardenBossStateReader` /
   `MoveSpeedModifier` cache `FieldInfo` and poke private state directly. Cheap and exact.
   Failure modes (field renamed, IL2CPP stripping) log a one-time warning and degrade.

2. **Companion component fallback** — `PhaseShockwaveDriver` is a runtime-added
   `MonoBehaviour` that emits shockwaves on its own timer (continuous or echo modes) using the
   prefab pulled from `WardenBoss` via reflection. Phase 3's "continuous shockwaves" run
   exclusively through this driver because no amount of slam-cadence tweaking can produce a
   fixed 1.5s interval through the existing state machine.

If you'd rather avoid reflection entirely in a future refactor, expose:

- `public Vector2 SlamInterval { get; set; }` on `WardenBoss`
- `public GameObject ShockwavePrefab => shockwavePrefab;` on `WardenBoss`
- A public state event (e.g. `event Action OnSlam;`) on `WardenBoss`
- `public Vector2 Size { get; set; }` on `ArenaBounds`

…and the phase code can be simplified to direct property access.

## Prefab wire-up

On the Warden boss prefab:

1. Add `BossEncounter` — leave `PhaseSet = Warden`.
2. Add `BossDeathSequence` — optionally assign `guaranteedDropPrefab` to a `BombPickup` or
   `HealthPickup` from `Assets/Scripts/Pickups`.
3. Optionally add `BossArenaSetup` and tune `bossFightSize`.

No other scene wiring is needed; the framework auto-resolves `EnemyHealth`, `WardenBoss`,
`EnemyChaseAI`, and `ArenaBounds` via `TryGetComponent` / `GetComponentInParent`.
