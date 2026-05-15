# LoneFighter — Editor Setup Checklist

This repo ships **code-complete** for a Vampire-Survivors-style arena game, plus project settings and packages. Scene composition, prefab authoring, ScriptableObject instances, and particle prefabs have to be wired up once in the Unity Editor — that's what this file is for.

Plan on ~15–30 minutes for the first-time setup.

## 1. Open the project

1. Install **Unity 6 LTS (6000.0.x)** via Unity Hub.
2. `Add project from disk` → select this repository folder.
3. First import will take ~3–5 min. If Unity prompts to switch input system, choose **New Input System Only**.

## 2. Confirm URP + 2D Renderer

If Unity didn't auto-create the URP assets, do it now:

1. `Assets/Settings/` → right-click → **Create → Rendering → URP Asset (with 2D Renderer)**.
2. Set **Project Settings → Graphics → Scriptable Render Pipeline Settings** to that asset.
3. Set **Project Settings → Quality → Render Pipeline Asset** to the same.

## 3. Drop in art

Recommended free CC0 pack: **[Kenney Tiny Dungeon](https://kenney.nl/assets/tiny-dungeon)** or **[Kenney 1-Bit Pack](https://kenney.nl/assets/1-bit-pack)**.

1. Extract into `Assets/Sprites/`.
2. For each sprite: set **Pixels Per Unit = 16**, **Filter Mode = Point (no filter)**, **Compression = None** if you want crisp pixels.
3. Build a Sprite Atlas per category (`Assets → Create → 2D → Sprite Atlas`) and add the sprite folders — keeps draw calls low.

## 4. Player prefab (`Assets/Prefabs/Player.prefab`)

Empty GameObject → add components:

- `SpriteRenderer` (sorting layer = `Player`)
- `Rigidbody2D` (Body Type = Dynamic, Gravity Scale = 0, Freeze Rotation Z, Interpolate)
- `CapsuleCollider2D` or `CircleCollider2D` (small, around the body)
- `PlayerInput` component → Actions = `Assets/Settings/InputActions.inputactions`, Default Map = `Player`, Behavior = **Send Messages**
- `PlayerController`, `PlayerHealth`, `PlayerLevel`
- Child GameObject `MagnetZone` with a larger `CircleCollider2D` (Is Trigger ✓) + `XPCollector`
- Child GameObject `Weapons` with `WeaponInventory`. Drag a `WeaponData` SO into the **Starting Weapon** slot.

Set the player to layer **Player**.

## 5. Enemy prefab

Empty GameObject → `SpriteRenderer` + `Rigidbody2D` (Dynamic, no gravity) + `CircleCollider2D` + `EnemyBase` + `EnemyHealth` + `EnemyChaseAI`. Layer = **Enemy**. Drop an `EnemyData` SO into the **Data** slot, drop the **XP Gem prefab** into **Xp Gem Prefab**.

## 6. Projectile prefab

Empty GameObject → small `SpriteRenderer` + `Rigidbody2D` (Dynamic, gravity 0) + `CircleCollider2D` (Is Trigger ✓) + `Projectile`. Layer = **Projectile**. Trail Renderer optional (looks great with bloom).

## 7. XP Gem prefab

Empty GameObject → `SpriteRenderer` + `CircleCollider2D` (Is Trigger ✓) + `XPGem`. Layer = **Pickup**.

## 8. Physics layer matrix

`Project Settings → Physics 2D → Layer Collision Matrix`:

- **Projectile** ✗ Projectile, ✗ Pickup
- **Pickup** ✗ Pickup, ✗ Projectile, ✗ Enemy
- **Enemy** ✓ Enemy (or ✗ if you want clean swarms), ✓ Player, ✓ Projectile

## 9. ScriptableObject content

Create at least:

- `Assets/Data/Weapons/Pistol.asset` — drag the **Projectile prefab** into `projectilePrefab`.
- `Assets/Data/Enemies/Grunt.asset` — drag the **Enemy prefab** into `prefab`.
- `Assets/Data/Upgrades/*.asset` — three or more (e.g. Bigger Bullets +25% damage, Quick Hands −15% cooldown, Vitality +20% max HP, Fleet Foot +10% speed, Magnet +30% radius).
- `Assets/Data/Waves/Wave1.asset` — set `runDuration = 300`, add entries (e.g. 0→60s: Grunt 1/sec, 60→180s: Grunt 3/sec, 180→300s: Grunt 6/sec).

## 10. Particle prefabs (the **juice**)

Build five `ParticleSystem` prefabs in `Assets/Prefabs/Fx/`:

| Prefab | Description |
| --- | --- |
| `Fx_EnemyExplosion` | Burst 24 particles, lifetime 0.4s, start speed 4–8, size 0.15→0, bright color over lifetime, **Stop Action: Disable**. |
| `Fx_ProjectileImpact` | Burst 8 sparks, lifetime 0.2s, start speed 2–4. |
| `Fx_PlayerHit` | Radial flash, lifetime 0.25s, red→white. |
| `Fx_XpPickup` | Soft glow + 6 tiny sparks, lifetime 0.3s. |
| `Fx_LevelUp` | 60 particles ring burst, lifetime 0.8s, yellow→cyan, **Renderer: Stretched Billboard** for streaks. |

For each: set **Renderer Material** to a URP Sprite-Lit/Unlit with **Additive** blending and **HDR** colors (intensity 2–4) so Bloom pops.

## 11. FX rig in `Game.unity`

1. Add empty GameObject `FxService` → add `FxService` component.
2. Drag the five `Fx_*` prefabs into its slots, plus a `DamagePopup` prefab (see below).
3. Add `CinemachineImpulseSource` component → assign in `FxService.impulseSource`.
4. Add `CinemachineImpulseListener` to the main camera so the impulses actually shake.

## 12. Damage popup prefab

World-space Canvas → child with `TextMeshProUGUI` + `DamagePopup`. Font, big bold, additive shader. Drag into `FxService.damagePopupPrefab`.

## 13. Camera + Cinemachine

1. Main Camera → orthographic, size 6, background black or dark.
2. `Create → Cinemachine → 2D Camera` → set **Tracking Target = Player**, `Dead Zone` 0.05.
3. Add **Cinemachine Impulse Listener** to the camera.

## 14. Game scene (`Assets/Scenes/Game.unity`)

Hierarchy:

- `Main Camera` (+ CinemachineBrain) + `Cinemachine Camera` following Player
- `Player` instance
- `EnemySpawner` GameObject (drag XPGem prefab into `xpGemPrefab`)
- `WaveManager` GameObject (drag the `WaveConfig` SO + `EnemySpawner` reference)
- `GameManager`, `PoolService`, `UpgradeService` (drag your `UpgradeData` SOs into its pool), `AudioManager`, `FxService` — all on one `Systems` parent
- `HUD Canvas` (Screen Space Overlay, portrait reference resolution 1080×1920) containing:
  - `HudController` host GameObject with HP slider, XP slider, TMP level/timer/kills
  - `LevelUpModal` panel (3 child buttons hooked into `LevelUpModal.choices`)
  - `PauseController` panel with Pause/Resume/Menu buttons
  - Bottom-left: `OnScreenStick` (from `com.unity.inputsystem.UI`) → set **Control Path** to `<Gamepad>/leftStick`. The Player's `PlayerInput` will receive the stick value through `Move` via the new Input System.

## 15. Main menu + game over scenes

- `MainMenu.unity`: Canvas + Play / Quit buttons + `MainMenuController`.
- `GameOver.unity`: Canvas + TMP summary + Retry / Menu buttons + `GameOverController`.

Add all three scenes to **File → Build Settings → Scenes In Build** in the order: MainMenu → Game → GameOver.

## 16. Post-processing for the "expensive" look

1. Add a **Global Volume** to `Game.unity` (Component → Volume → Global Volume) → New profile.
2. Add overrides:
   - **Bloom**: Threshold 0.9, Intensity 1.0, Scatter 0.7 — this is where the additive HDR particles pay off.
   - **Vignette**: Intensity 0.25, Smoothness 0.4.
   - **Chromatic Aberration**: 0.15 (subtle).
   - **Color Adjustments**: Post-exposure +0.2, Saturation +15.
3. URP Asset → **Quality → HDR ✓**, **Post Processing ✓**.

## 17. Mobile build settings

- `File → Build Settings → Switch Platform → Android` (or iOS).
- **Player Settings → Resolution and Presentation → Orientation → Portrait**.
- **Player Settings → Other Settings → Color Space → Linear**.
- **Player Settings → Other Settings → Graphics APIs**: prefer Vulkan (Android) / Metal (iOS).
- **Quality → V Sync Count → Don't Sync** (we control frame rate from `GameManager.Awake`).

## 18. Play

Open `Game.unity` → set Game view to **1080×1920 portrait** → press **Play**. Use **WASD** or a gamepad to verify movement in Editor. On device, the on-screen stick drives `Move`.

## 19. Performance tips for 120Hz mobile

- Keep total active enemies ≤ ~200 (already capped in `EnemySpawner.globalCap`).
- Pool every transient: enemies, projectiles, gems, particle FX, damage popups (already wired through `PoolService` / `ParticleAutoRelease`).
- For ParticleSystems, prefer **Burst** with short lifetime + low simulation cost over long-lived continuous emissions.
- Profile on device early — `Window → Analysis → Profiler` connected over USB.

That's it. Once wired, the gameplay loop should fully work end-to-end.
