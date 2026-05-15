# MenuBackground

Visual life for the MainMenu scene: scrolling background, drifting particles,
a silent fake-combat sim, a breathing title, and a hover/touch FX layer on
buttons. All scripts live under namespace `LoneFighter.UI.MenuBackground`.

Everything is **purely visual** — no gameplay code, no audio, no physics.

## Files

| Script | Role |
| --- | --- |
| `MenuBackgroundController.cs` | Top-level driver. Owns/auto-finds the sub-effects and gates `MenuFakeCombat` on the `LF_MenuFxIntensity` PlayerPref. |
| `ParallaxScroller.cs` | Slow diagonal scroll of a background sprite using `Mathf.Repeat` on the material's UV offset (with a transform-translate fallback). |
| `MenuFakeCombat.cs` | Silent visual sim. Spawns wandering enemy sprites toward a fake player, fires fake projectiles, plays small `ParticleSystem` bursts on hit. |
| `TitleLogoPulse.cs` | Animates a `TMP_Text` title: scale 1.0 ↔ 1.04 on a 2.5 s sine wave + slow color shift between two palette stops. |
| `MenuParticleField.cs` | Ambient world-space `ParticleSystem` (dust motes + optional ember sub-system). |
| `MenuButtonHoverFx.cs` | Subtle glow + scale on UI Buttons via runtime-added `EventTrigger` (PointerEnter/Exit, PointerDown/Up, Select/Deselect). |

## Scene wire-up (MainMenu)

Suggested hierarchy under the `MainMenu` scene:

```
MainMenu (existing root)
├── MenuBackground                      [+ MenuBackgroundController]
│   ├── Parallax                        [SpriteRenderer + ParallaxScroller]
│   ├── ParticleField                   [ParticleSystem + MenuParticleField]
│   │   └── Embers (optional)           [ParticleSystem]   ← assigned to MenuParticleField.embers
│   └── FakeCombat                      [+ MenuFakeCombat]
│       └── (FakePlayer auto-created at runtime if not assigned)
└── Canvas
    ├── Title (TMP)                     [+ TitleLogoPulse]
    └── Buttons
        ├── PlayButton                  [+ MenuButtonHoverFx]
        ├── SettingsButton              [+ MenuButtonHoverFx]
        └── QuitButton                  [+ MenuButtonHoverFx]
```

Steps:

1. **Create `MenuBackground` GameObject** at the MainMenu scene root.
   Add `MenuBackgroundController`. Leave its sub-effect refs empty — it will
   auto-locate any matching component on its children at `Awake`.
2. **Parallax child** — `SpriteRenderer` with your background sprite. The
   sprite's texture should use **Wrap Mode = Repeat** in its import settings
   for the UV scroll path. Add `ParallaxScroller`.
3. **ParticleField child** — `ParticleSystem` + `MenuParticleField`. To get
   embers too, add a second `ParticleSystem` child and drag it into the
   `Embers` slot.
4. **FakeCombat child** — empty GameObject + `MenuFakeCombat`. Assign a small
   "enemy" `Sprite`, a "player" `Sprite`, a "projectile" `Sprite`, and
   (optional) a `hitBurstPrefab` ParticleSystem. The script will auto-build
   a placeholder burst if none is provided.
5. **Title** — TMP text under the Canvas, add `TitleLogoPulse`. The component
   auto-fetches its `TMP_Text` if left unset.
6. **Buttons** — drop `MenuButtonHoverFx` on each Button. If you want a glow,
   nest a soft-glow `Image` underneath the button graphic and drag it into
   the `glowGraphic` slot.

## Intensity gating

`MenuBackgroundController` reads `PlayerPrefs.GetString("LF_MenuFxIntensity", "medium")`.
When the value is `"low"`, `MenuFakeCombat` is disabled on enable and its
GameObject deactivated. Call `MenuBackgroundController.RefreshIntensity()`
after the settings menu mutates that pref to re-apply the gate without
reloading the scene.

## Performance notes

- All loops use `Mathf.Repeat` / accumulated offset — no per-frame allocations.
- `MenuFakeCombat` caps at `maxEnemies` (default 8) and reuses internal pools.
- Particle systems use **world** simulation space, matching the in-game look.
- `TitleLogoPulse` and `MenuButtonHoverFx` use `Time.unscaledDeltaTime`, so
  they continue to animate when the menu pauses gameplay time.

## Not touching existing files

This module is fully additive. It contains no references to existing
`MainMenuController` code paths — wire it via the Inspector only.
