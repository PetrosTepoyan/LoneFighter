# World layer

Everything that makes the arena feel like a *place* lives here: soft bounds, a
parallax/star backdrop, environmental hazards, and the editor tools that scaffold
them into a scene.

Gameplay scripts under `Assets/Scripts/World/` use namespace `LoneFighter.World`.
Editor scripts under `Assets/Editor/World/` use `LoneFighter.EditorTools.World`.
New pickup types live next to the existing ones in `Assets/Scripts/Pickups/` to
stay in the `LoneFighter.Pickups` namespace and reuse the `Pickup` base.

## Components at a glance

| Script | Role |
| --- | --- |
| `ArenaBounds.cs` | Soft, elastic wall pushback on the player at the arena edge. |
| `ParallaxBackground.cs` | Drives multiple background `Transform` layers at fractional speeds, wrapping every `tileSize` units. |
| `StarfieldBackground.cs` | Procedurally spawns a fixed pool of tiny sprite "stars" that follow the camera. |
| `Hazard.cs` | Abstract base for AoE damage zones (trigger + tick cadence). |
| `PoisonPuddle.cs` | Pooled `Hazard` subclass — lingering damage circle, spawned by Spitters. |
| `Pickups/HealthPickup.cs` | Heals the player by a flat amount on collection. |
| `Pickups/MagnetPickup.cs` | Drags every active `XPGem` onto the player. |
| `Pickups/BombPickup.cs` | Heavy AoE damage to enemies around the player + screen-shake juice. |

## Scene wiring

1. **Open Game.unity.**
2. Run **`LoneFighter → World → Arena Setup`** (see `Assets/Editor/World/ArenaPresetWindow.cs`).
   Creates this hierarchy in the active scene:

   ```
   Arena
     ├─ Bounds          (ArenaBounds — center (0,0), size (30,30))
     ├─ Background      (ParallaxBackground)
     │    └─ Starfield  (StarfieldBackground, optional)
     └─ Hazards         (empty parent for runtime puddles, etc.)
   ```

3. Run **`LoneFighter → World → Generate Arena Background`** to bake a 256×256
   dark-grid sprite to `Assets/Sprites/Generated/ArenaGrid.png`. Then in the
   scene:
   - Add a `SpriteRenderer` GameObject under `Arena/Background`
   - Assign the generated sprite, set **Draw Mode = Tiled**, **Size = 30 × 30**
   - Sorting Layer = `Background` (or `Order in Layer = -50`).

4. **ParallaxBackground layers.** For each extra parallax layer:
   - Create a child of `Arena/Background` with a SpriteRenderer (tiled).
   - Drag those Transforms into `ParallaxBackground.layers` (back-to-front order).
   - Set a matching `parallaxFactors` entry per layer. Suggested values:
     - Deepest stars: `0.05`
     - Mid stardust: `0.2`
     - Floor grid: `0.6`
     - Decals/dust: `0.9`
   - `tileSize` should match the world-space size of your tiled sprite (e.g. `20`).

5. **Hazards.** `PoisonPuddle` is built to be pooled. Author a prefab:
   - SpriteRenderer (a green-ish blob)
   - `CircleCollider2D` (Is Trigger ✓)
   - `PoisonPuddle` component
   Then have Spitter enemies (see BALANCE.md row 90s+) call
   `PoolService.Instance.Get(poisonPuddlePrefab, hitPos, Quaternion.identity)`
   and optionally `puddle.Configure(radius, damage, interval, life)` to tune.

## Arena size: why 30 × 30?

Game camera ortho size 6 (per `SETUP.md` §13) means the camera sees ~12 units
vertically. A 30 × 30 playable rect is **~2.5 screens wide and tall** — wide
enough that the player has meaningful room to kite and circle the swarm, but
small enough that:

- `ArenaBounds` pushback bites well before enemies despawn at the edge of the
  off-camera spawn ring.
- The player never spends long stretches of a 5-minute run walking through empty
  space — the arena always feels populated.
- A single 30 × 30 tiled SpriteRenderer is enough to cover the floor (no
  Tilemap setup needed).

If you go larger (e.g. 50 × 50), bump `softMargin` to `2.5–3` so the wall still
*feels* soft at the higher absolute distance, and increase `pushbackStrength`
proportionally.

## ArenaBounds tuning

- `pushbackStrength = 8` works against the player's default `moveSpeed = 5`.
  At 8, the player still feels in control near the wall; bump to ~12 if you
  want a stiffer feel for late-game knockback.
- `softMargin = 1.5` gives a visible "you're at the edge" band of ~1.5 units.
- `hardClampMargin = 4` is a safety net — only triggered by big knockbacks /
  scene-load placement, not by normal movement.

## Pickup tuning notes

These three pickups are intentionally rarer than `XPGem` — they're build-shifting
moments, not constants.

| Pickup | Suggested drop weight | Typical tuning |
| --- | --- | --- |
| `HealthPickup` | ~1.5% of kills | `healAmount = 25` (≈25% of base HP). |
| `MagnetPickup` | ~0.7% of kills | No knobs — sucks the whole field. |
| `BombPickup` | ~0.3% of kills | `explosionRadius = 8`, `damage = 9999` (one-shots non-bosses). |

Wire them into enemy death rolls the same way `EnemyBase.HandleDeath` already
handles `xpGemPrefab` — give your `EnemyData` a `specialDropPrefab` and rolled
chance, then `PoolService.Instance.Get(prefab, transform.position, ...)`.

All three drop pickups follow the same lifecycle as `XPGem`: poll distance in
`Update`, fire FX, return to pool. They share the existing `Pickup` base so the
layer/collider setup is identical (`Layer = Pickup`, trigger Collider2D).

## Editor tools

| Menu | Behavior |
| --- | --- |
| `LoneFighter → World → Arena Setup` | Window with options + a "Create Arena in Active Scene" button. Idempotent — running it twice doesn't duplicate the hierarchy. |
| `LoneFighter → World → Generate Arena Background` | Bakes `Assets/Sprites/Generated/ArenaGrid.png` (256×256, PPU 16, Point, Clamp) and prints follow-up steps in the Console. |

Both tools are pure scaffolding — feel free to delete the generated GameObjects
and start over; nothing in the runtime systems depends on them being present.
