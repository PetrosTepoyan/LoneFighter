# Advanced Enemies

Namespace: `LoneFighter.Enemies.Advanced`
Folder: `Assets/Scripts/Enemies/Advanced/`

Three new enemy archetypes that force the player to adapt tactically. Each is implemented as
sibling MonoBehaviours that attach to the existing `EnemyBase` / `EnemyHealth` / Rigidbody2D
stack — no existing files are modified. New prefabs and `EnemyData` ScriptableObjects need to be
authored in the Unity editor.

> Hard rule honored: every component lives in this folder and subscribes to existing public
> events (`EnemyHealth.OnDied`) or toggles existing public flags (`EnemyChaseAI.enabled`). No
> existing source file is changed.

---

## 1. HealerEnemy — back-line support

**Components on prefab:**

- `EnemyBase` (existing, required)
- `EnemyHealth` (existing, required)
- `EnemyChaseAI` (existing, required-by-`EnemyBase`) — **leave it disabled in the inspector**
- `EnemyKeepDistanceAI` (existing) — **the active mover**
- `HealerEnemy` (new) — heal pulse logic
- `HealerPriorityIndicator` (new) — floating "PRIORITY" marker
- `Rigidbody2D` + `Collider2D` (Unity)
- `SpriteRenderer` child (Unity)

**Behavior:**

- Pairs with `EnemyKeepDistanceAI`, so the healer stays at ~5-7 units from the player.
- Every `pulseInterval` seconds (default **2.0s**) it sweeps `EnemyRegistry.Enemies`, picks every
  ally within `healRadius` (default **3.5u**) and restores `healAmountPerPulse` HP (default
  **6**) up to each ally's `Max`.
- The pulse spawns a `HealerAura` telegraph at the healer's position; per-target ticks can also
  spawn a smaller `HealerAura`.
- High-priority kill target: `HealerPriorityIndicator` adds a bobbing icon above the healer that
  tints red as the player gets close.

**`EnemyData` spec (suggested):**

| field             | value         |
| ----------------- | ------------- |
| displayName       | `Mender`      |
| maxHealth         | `28`          |
| moveSpeed         | `2.2`         |
| contactDamage     | `4`           |
| contactCooldown   | `0.6`         |
| xpDrop            | `4`           |
| gemDropChance     | `1.0`         |
| spriteScale       | `1.0`         |
| tint              | mint green    |
| projectilePrefab  | _none_        |
| aoeRadius         | `0`           |

**Healing-API workaround (important):**

`EnemyHealth` in this project does **not** expose a public `Heal(float)` method and its `Current`
setter is private. To respect the "do not modify existing files" rule, `HealerEnemy` heals by
calling `EnemyHealth.ApplyDamage(-amount)` with the amount pre-clamped to `Max - Current` so it
never over-heals. The relevant lines in the canonical `EnemyHealth`:

```csharp
public void ApplyDamage(float amount)
{
    if (_dead) return;
    Current -= amount;
    if (Current <= 0f) { _dead = true; OnDied?.Invoke(this); _enemy.HandleDeath(); }
}
```

`Current -= (-amount)` ⇒ `Current += amount`. Dead enemies short-circuit safely. If
`EnemyHealth.Heal(float)` is added later, swap the single call in `HealerEnemy.ApplyHeal()`.

---

## 2. SplitterEnemy — one-generation death-spawner

**Components on the parent splitter prefab:**

- `EnemyBase` (existing)
- `EnemyHealth` (existing)
- `EnemyChaseAI` (existing)
- `SplitterEnemy` (new) — listens to its own `EnemyHealth.OnDied` and spawns shards
- `Rigidbody2D` + `Collider2D` + visible `SpriteRenderer` (Unity)

**Components on the shard prefab (`SplitterEnemy_Small`):**

- `EnemyBase`, `EnemyHealth`, `EnemyChaseAI` (existing)
- `SplitterSmall` (new) — marker so we _know_ this is a shard
- **Do NOT** put `SplitterEnemy` on this prefab. That is what enforces the
  one-generation-only rule. Two distinct prefabs, no shared chain.

**Behavior:**

- The component subscribes to `EnemyHealth.OnDied` in `OnEnable` and unsubscribes in `OnDisable`.
- When the parent dies, `OnDied` fires synchronously inside `ApplyDamage` (before
  `EnemyBase.HandleDeath` pools the object). The handler captures the corpse's world position and
  sets a pending-split flag.
- The actual shard spawn happens in `OnDisable` — by then `HandleDeath` has run, the object is
  about to be parked in the pool, and there's no risk of re-entering the pool during a callback.
- Shards are spawned at `spawnSpread` (default **0.6u**) around the death position, evenly
  distributed in angle, with an outward burst impulse.

**`EnemyData` spec (parent Splitter):**

| field             | value      |
| ----------------- | ---------- |
| displayName       | `Splitter` |
| maxHealth         | `40`       |
| moveSpeed         | `2.0`      |
| contactDamage     | `6`        |
| contactCooldown   | `0.5`      |
| xpDrop            | `5`        |
| gemDropChance     | `1.0`      |
| spriteScale       | `1.4`     (visibly larger, cracked tint) |
| tint              | rusty orange |

**`EnemyData` spec (shard `SplitterEnemy_Small`):**

| field             | value         |
| ----------------- | ------------- |
| displayName       | `Splitter Shard` |
| maxHealth         | `10`          |
| moveSpeed         | `2.6`         (faster but fragile) |
| contactDamage     | `3`           |
| contactCooldown   | `0.5`         |
| xpDrop            | `1`           |
| gemDropChance     | `0.6`         |
| spriteScale       | `0.6`         |
| tint              | darker rust   |

**Authoring step:** populate `SplitterEnemy.shardData` on the parent prefab with the shard
`EnemyData` asset (whose `prefab` field points at the shard prefab). Forgetting to set this
silently disables splitting.

---

## 3. TeleporterEnemy — vanish-and-strike stalker

**Components on prefab:**

- `EnemyBase`, `EnemyHealth`, `EnemyChaseAI`, `Rigidbody2D`, `Collider2D`, `SpriteRenderer`
  child (existing)
- `TeleporterEnemy` (new) — state machine driving the whole cycle

**Optional VFX prefabs:**

- `TeleportFlash` (new) — purple particle/sprite burst, spawned on vanish AND reappear
- `TeleportTelegraph` (new) — red expanding warning ring, spawned at the reappear position and
  driven across the windup

**State cycle (~5–7s total):**

```
Visible (2.0s)
  -> Vanishing (0.35s, alpha 1→0, chase off, collider off)
  -> Hidden (0.5s, no chase, no damage)
  -> Reappearing (0.35s, teleports near player, alpha 0→1, collider still off)
  -> Attacking (0.6s windup with red ring + half-window lunge, collider re-enabled)
  -> Cooldown (0.7s, chase resumes)
  -> Visible …
```

Defaults sum to **4.5s** for the inner loop plus the configurable `visibleDuration` (default
2.0s) ⇒ **6.5s** cycle, inside the 5–7s spec.

**Key design notes:**

- `EnemyChaseAI` is toggled via its `enabled` property — we never modify `EnemyChaseAI` itself.
- The body collider is also disabled while invisible so the player can't be cheap-shotted by
  walking into a ghost.
- Teleport position is a random angle 4–6 units around the player (`teleportMinDistance` /
  `teleportMaxDistance`).
- The attack is an instantaneous AOE check at the end of the windup (`attackRadius` /
  `attackDamage`), plus an optional short lunge during the second half of the windup so the
  enemy visibly commits.

**`EnemyData` spec:**

| field             | value           |
| ----------------- | --------------- |
| displayName       | `Phaser`        |
| maxHealth         | `32`            |
| moveSpeed         | `2.4`           |
| contactDamage     | `4`             (used only while Visible) |
| contactCooldown   | `0.5`           |
| xpDrop            | `5`             |
| gemDropChance     | `1.0`           |
| spriteScale       | `1.0`           |
| tint              | violet          |
| aoeRadius         | `0`             (the Teleporter handles its own AOE; this field is unused) |

---

## Required prefab pairings — quick checklist

- **HealerEnemy prefab:**
  - `EnemyKeepDistanceAI` enabled, `EnemyChaseAI` disabled (still present so `EnemyBase`'s
    `RequireComponent` is satisfied)
  - `HealerEnemy` and `HealerPriorityIndicator` attached
  - Optional: `pulseTelegraphPrefab` and `healAuraPrefab` referencing a `HealerAura` prefab
- **SplitterEnemy parent prefab:**
  - `SplitterEnemy` attached, `shardData` populated with the shard `EnemyData`
  - The shard `EnemyData.prefab` must reference the small shard prefab
- **SplitterEnemy_Small prefab:**
  - `SplitterSmall` attached, NO `SplitterEnemy`
- **TeleporterEnemy prefab:**
  - `TeleporterEnemy` attached
  - Optional: `teleportFlashPrefab` (with `TeleportFlash`) and `teleportTelegraphPrefab`
    (with `TeleportTelegraph`)
  - `EnemyChaseAI` enabled (the Teleporter toggles it itself)

## Spawner wiring

Each of the three top-level prefabs needs an `EnemyData` asset in the project (e.g.
`Assets/Data/Enemies/`). Adding them to `EnemySpawner`'s scheduling table (per the existing
spawner UX in this repo) will get them appearing in waves.
