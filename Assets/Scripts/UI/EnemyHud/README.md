# EnemyHud

Optional in-world health bars and elite/boss name labels for `LoneFighter.Enemies`. All
files in this folder live under the `LoneFighter.UI.EnemyHud` namespace and do **not**
modify any existing gameplay or UI code — the only public coupling is to `EnemyBase`,
`EnemyHealth`, `EnemyData.displayName`, `EnemyRegistry`, and `WardenBoss`.

## Files

| File | Purpose |
|---|---|
| `EnemyHudSettings.cs` | Static, PlayerPrefs-backed toggle for the small per-enemy bars. Key: `LF.EnemyHud.ShowHealthBars` (default `false`). Independent of `GameSettings`. |
| `EnemyHealthBar.cs`   | World-space `Canvas` widget that follows a single enemy. Slim red fill + darker outline. Fades in over `0.2s` on damage, fades out `2.0s` after the last hit. |
| `EnemyHealthBarSpawner.cs` | Singleton that watches `EnemyRegistry` each frame, binds pooled bars to new enemies, and releases bars when enemies disappear. Skips bosses (they get a `BossHealthBar`). |
| `BossHealthBar.cs`    | Screen-locked golden bar pinned to the top of the HUD. Auto-discovers a boss in `EnemyRegistry` or accepts an external `Bind(EnemyHealth)`. Always visible while the boss lives. |
| `EnemyLabelHud.cs`    | Optional `TMP_Text` label like `ELITE GRUNT` / `WARDEN BOSS`. Per-prefab inspector flag (`showLabel`) and archetype dropdown. |

## How the toggle works

`EnemyHudSettings.ShowEnemyHealthBars` is a static `bool` that reads/writes the
`LF.EnemyHud.ShowHealthBars` PlayerPref. Flip it from a settings UI:

```csharp
using LoneFighter.UI.EnemyHud;
EnemyHudSettings.ShowEnemyHealthBars = true; // or false
```

When the value changes, `EnemyHudSettings.Changed` fires. `EnemyHealthBarSpawner`
listens and releases every active bar back to its pool when the toggle flips off.
The boss bar is **not** gated by this toggle — `BossHealthBar` is always-on while
a boss is alive.

## Why polling instead of an `OnDamaged` event

`EnemyHealth` exposes `OnDied` but does not expose an `OnDamaged` event. The hard
rule of this contribution is "no modifications to existing files," so both
`EnemyHealthBar` and `BossHealthBar` poll `EnemyHealth.Current` once per frame
and compare it against the previously-seen value. Any decrease is treated as
fresh damage and resets the fade-in timer. The cost is one float compare per
bound bar per frame — negligible for the mobile target.

If `EnemyHealth` gains an `OnDamaged` event in the future, you can remove the
polling block in `EnemyHealthBar.Tick` and `BossHealthBar.Update` without
touching anything else.

## Prefab setup

### Small per-enemy bar (`EnemyHealthBar`)

1. **Create a prefab** named `EnemyHealthBar` under `Assets/Prefabs/UI/`.
2. Root GameObject: add a `Canvas` (Render Mode = **World Space**), a
   `CanvasScaler` (Constant Pixel Size, scale ~0.005), a `CanvasGroup`, and the
   `EnemyHealthBar` component.
3. Child `Image` named `Background` — black with ~70% alpha, sized e.g. 64x10.
4. Child `Image` named `Fill` — red, `Image Type = Filled`, `Fill Method = Horizontal`,
   `Fill Origin = Left`, anchored to the background, inset by 1–2 px for the outline.
5. Drag `Fill` into `EnemyHealthBar.fillImage` and `Background` into `backgroundImage`.
6. Configure `worldOffset` (default `(0, 0.6, 0)`) and `useScreenSpaceTracking`:
   - **Off (default)**: the bar is positioned in world space at `enemy.position + worldOffset` every `LateUpdate`. Use this when you instantiate the bar as a separate floating widget (the spawner does this).
   - **On**: bar position is computed via `Camera.main.WorldToScreenPoint` and then projected back to world coords on the canvas plane. Use this if you want the bar to read truly screen-pixel-locked regardless of camera distance.

The fade timings (`fadeInDuration`, `hideDelay`, `fadeOutDuration`) are inspector-tunable.

### Spawner (`EnemyHealthBarSpawner`)

1. Create an empty GameObject in the gameplay scene named `_EnemyHealthBarSpawner`.
2. Add the `EnemyHealthBarSpawner` component.
3. Drag the prefab from above into `healthBarPrefab`.
4. (Optional) Create a child empty named `BarPool` and assign it to `poolParent`
   so instantiated bars don't clutter the root hierarchy.
5. Set `prewarmCount` (default 16) and `maxActive` (default 128) to taste for
   your wave sizes.
6. Edit the `bossKeywords` list if you add boss `EnemyData` whose names don't
   contain `"boss"` or `"warden"` — those keywords are matched against both the
   asset name and `EnemyData.displayName`.

The spawner is a singleton (`EnemyHealthBarSpawner.Instance`) but it does not
`DontDestroyOnLoad` itself — it's scene-scoped, which is correct for gameplay
scenes that already manage their own lifetime.

### Boss bar (`BossHealthBar`)

1. Add an empty GameObject under your screen-space HUD canvas named `BossHealthBar`,
   anchored to the **top-center** of the safe area.
2. Add a `CanvasGroup` and the `BossHealthBar` component.
3. Inside it, create a `Background` `Image` (dark, ~600x32) and a `Fill` `Image`
   (golden, `Image Type = Filled`, `Fill Method = Horizontal`).
4. (Optional) Add a `TMP_Text` child named `Name` for the boss's display name.
5. Wire `fillImage`, `backgroundImage`, `nameLabel` (optional), and `canvasGroup`.
6. Leave `autoDiscoverByKeyword = true` if you want the bar to grab the first
   boss it sees in `EnemyRegistry`. Otherwise call:

   ```csharp
   BossHealthBar.FindObjectOfType<BossHealthBar>().Bind(myBossHealth);
   ```

   from whatever boss-spawning system you have.

### Elite / boss label (`EnemyLabelHud`)

1. Add a child `Canvas` (World Space) above the enemy sprite on the **elite or
   boss prefab** (e.g. the existing `WardenBoss` prefab).
2. Inside it, add a `TMP_Text` and wire it to `EnemyLabelHud.labelText`.
3. Tick `Show Label` and pick `Archetype = Elite` or `Boss`.
4. Leave `overrideText` blank to auto-generate from `EnemyData.displayName`
   (e.g. `WARDEN` → `WARDEN BOSS`), or set it to a literal string.

`EnemyLabelHud` runs `Refresh()` in `OnValidate` so the label updates live in
the editor as designers tweak the archetype dropdown.

## Wiring the toggle into the existing settings panel

The existing `SettingsPanel.cs` cannot be modified per this contribution's
rules. To expose the toggle to players, add a sibling `Toggle` in the settings
scene with this one-liner on its `OnValueChanged`:

```csharp
LoneFighter.UI.EnemyHud.EnemyHudSettings.ShowEnemyHealthBars = isOn;
```

Pre-populate the toggle's initial state from
`EnemyHudSettings.ShowEnemyHealthBars` in your panel's `OnEnable`.

## Performance notes

- Bars are pooled — flipping the toggle on/off does not churn allocations.
- Inactive bars have their world-space `Canvas` disabled, so they cost zero
  rebuild work while invisible.
- The spawner does one `HashSet`/`Dictionary` walk per frame; this is
  proportional to live enemy count, which already caps out well below 300 on
  the mobile target.
