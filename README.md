# LoneFighter

Top-down arena survival game for mobile — inspired by Pickle Pete / Vampire Survivors / Survivor.io.

You stand at the center of the map. Enemies spawn off-screen in waves and chase you. Your weapons auto-fire at the nearest enemy. Kill enemies, collect XP gems, level up, pick from three upgrades, survive the run.

## Tech

- **Engine**: Unity 6 LTS (`6000.0.x`)
- **Render**: URP 2D
- **Input**: New Input System (on-screen virtual stick on mobile, WASD/gamepad in Editor)
- **Camera**: Cinemachine 3 (follow + impulse for screen shake)
- **Target**: Android / iOS, portrait, **120 Hz** where the panel supports it

## Vibe

Heavy juice — bloom, additive HDR particles, screen shake on every kill, hit-stop on heavy events, damage popups, level-up bursts, expensive-feeling chaos at high frame rate.

## First-time setup

Code, packages, and project settings are scaffolded. Prefab/scene composition and ScriptableObject content must be wired up once in the Unity Editor.

See **[SETUP.md](./SETUP.md)** for the ~15-30 minute checklist.

## Project layout

```
Assets/
  Scripts/
    Player/      input-driven controller, HP, leveling, XP magnet
    Weapons/     base + auto-targeting projectile weapon, projectile, inventory
    Enemies/     base, chase AI, health, off-camera spawner, registry
    Pickups/     XP gems
    Systems/     GameManager state machine, WaveManager, PoolService, FxService, UpgradeService, AudioManager
    UI/          HUD, level-up modal, main menu, game over, pause, damage popup
    Data/        ScriptableObjects: WeaponData, EnemyData, UpgradeData, WaveConfig
    Utils/       small helpers
  Settings/      InputActions
  Data/          (you author SO instances here)
  Prefabs/       (you author prefabs here)
  Sprites/       (drop a CC0 sprite pack here)
  Scenes/        MainMenu, Game, GameOver
```

## Status

Vertical-slice scaffold. After you complete `SETUP.md` you should have:

- Movable player via on-screen stick
- Auto-targeting projectile weapon
- Wave-spawned enemies that chase and damage you
- XP gem drops, magnet, leveling, level-up upgrade picker
- HUD with HP / XP / timer / kills, pause menu, game over screen
- Screen shake, particle bursts, damage popups, hit-stop
