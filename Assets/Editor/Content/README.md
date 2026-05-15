# Content Seeder

An Editor tool that turns `LoneFighter.Data.ContentSeed` (plain C# constants) into real
ScriptableObject `.asset` files. Run it once after pulling and you skip ~15 minutes of
hand-typing numbers into inspectors.

Menu: **LoneFighter → Content → Content Seeder**

## What it generates

Everything is written under `Assets/Data/`:

| Source array in `ContentSeed`        | Output path                                  |
| ------------------------------------ | -------------------------------------------- |
| `Weapons[]`                          | `Assets/Data/Weapons/{Name}.asset`           |
| `Enemies[]`                          | `Assets/Data/Enemies/{Name}.asset`           |
| `Upgrades[]`                         | `Assets/Data/Upgrades/{Name}.asset`          |
| `WaveTimeline[]` + `RunDurationSeconds` | `Assets/Data/Waves/MainRun.asset`         |

Spaces in names are stripped, so `"Spread Shotgun"` becomes
`Assets/Data/Weapons/SpreadShotgun.asset`, and so on.

## Buttons

- **Seed All** — runs every section in order and then wires cross-references.
- **Seed Weapons** / **Seed Enemies** / **Seed Upgrades** / **Seed Wave Config** —
  per-section seeders for incremental work.
- **Wire Cross-References** — second pass that resolves the name-string links inside
  the seed:
  - `UpgradeData.weaponToGrant` for every `UpgradeKind.GrantWeapon` upgrade
  - `WaveConfig.entries[*].enemy` for every wave timeline entry
- **Open Output Folder** — pings `Assets/Data` in the Project window.

The window also shows a small status block with how many assets currently exist for
each section vs. how many `ContentSeed` defines.

## Workflow

1. Open the project in Unity.
2. **LoneFighter → Content → Content Seeder** → **Seed All**.
3. The seeder creates / updates assets under `Assets/Data/` and wires the
   string-keyed cross-references between them.
4. Drag prefab and sprite references onto the generated assets in the Inspector:
   - `EnemyData.prefab` — the enemy prefab (created separately).
   - `EnemyData.sprite` — optional preview sprite.
   - `WeaponData.projectilePrefab` — the projectile prefab for projectile-style
     weapons (Pistol, Spread Shotgun). Orbital Blade / Lightning Chain don't
     need one.
   - `WeaponData.icon` / `UpgradeData.icon` — optional UI icons.

   The seeder intentionally never touches these fields — they live on disk only after
   other agents / you author the corresponding prefabs and sprites, so overwriting them
   would clobber that work.

## Re-running is safe

The seeder is idempotent. On every run it:

- Reuses any existing `.asset` at the canonical path instead of recreating it (so the
  asset GUID and any references pointing at it survive).
- Overwrites the numeric / string fields it manages with the latest `ContentSeed`
  values.
- Leaves `prefab`, `sprite`, `icon`, and `projectilePrefab` references alone.
- Re-resolves cross-references by name in a second pass.

Missing references during the cross-reference pass produce `Debug.LogWarning`
messages — they aren't fatal. Typical example: you seeded upgrades before weapons;
the `GrantWeapon` upgrades will log warnings and leave `weaponToGrant` untouched
until you re-run **Seed Weapons** + **Wire Cross-References** (or just **Seed All**).

## Where the numbers come from

`Assets/Scripts/Data/ContentSeed.cs` is the canonical numeric spec and mirrors
`BALANCE.md`. The seeder reads that file at edit time — there is no JSON, no CSV,
no asset bundle. If you tweak `ContentSeed.cs` you should re-run **Seed All** to
push the new values onto disk.
