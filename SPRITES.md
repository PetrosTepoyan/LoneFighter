# Sprites — LoneFighter Procedural Placeholder Art

LoneFighter ships **without** any binary art assets in version control. Instead,
an Editor tool generates pixel-art placeholder sprites on demand, so a fresh
clone of the repo can be played-tested immediately with zero downloads.

---

## The Tool

Menu: **`LoneFighter → Art → Sprite Generator`**

Source: `Assets/Editor/Art/`
- `Palette.cs` — neon palette constants (saturated, bloom-friendly).
- `ShapeRasterizer.cs` — crisp (NO antialiasing) primitives over `Color32[]`.
- `ProceduralSpriteGenerator.cs` — one `Build*()` per art piece, returning `Texture2D`.
- `SpriteGeneratorWindow.cs` — `EditorWindow` with buttons that PNG-encode each
  texture, write under `Assets/Sprites/Generated/`, and configure the importer.

### Buttons
- **Generate All Sprites** — produces every placeholder in one click.
- One **per-piece** button — regenerate a single sprite (useful while tweaking
  the palette or `Build*` method).
- **Open Output Folder** — reveals the `Generated/` folder in your OS file manager.

### What gets generated

| File                      | Size  | Notes |
|---------------------------|-------|-------|
| `Player.png`              | 32×32 | Cyan disc + light highlight + white forward notch (+X = forward) |
| `EnemyGrunt.png`          | 24×24 | Red disc, basic swarm |
| `EnemyRunner.png`         | 24×24 | Orange chevron, +X = facing |
| `EnemyTank.png`           | 40×40 | Crimson rounded square with armor studs |
| `EnemySpitter.png`        | 28×28 | Acid green body + cannon protrusion |
| `EnemyDasher.png`         | 28×28 | Magenta chevron silhouette |
| `EnemyBomber.png`         | 28×28 | Orange disc + white fuse + spark dot |
| `EnemyWarden.png`         | 56×56 | Purple hex + golden core, mini-boss read |
| `Projectile.png`          | 12×24 | White-hot core, yellow halo — additive streak |
| `XpGem.png`               | 16×16 | Green diamond + cyan inner sparkle |
| `ExplosionMask.png`       | 64×64 | Soft radial white→transparent (additive) |
| `SparkMask.png`           | 16×32 | Vertical white streak, billboard-friendly |
| `UiCircleIcon.png`        | 64×64 | Soft white disc — upgrade-icon base layer |

### Import settings applied per file
- `textureType = Sprite (Single)`
- `pixelsPerUnit = 16`
- `filterMode = Point` (NO bilinear smoothing — keep pixels crisp)
- `wrapMode = Clamp`
- `alphaIsTransparency = true`
- `mipmapEnabled = false`
- `textureCompression = Uncompressed`
- `spritePivot = (0.5, 0.5)` (Center alignment)

> **Implementation choice:** the tool calls `AssetImporter.GetAtPath` directly
> after each `ImportAsset` rather than using a global `AssetPostprocessor`. This
> avoids accidentally re-importing every other texture in the project. If you
> ever want the rule to also apply to hand-drawn PNGs you drop into
> `Assets/Sprites/Generated/`, swap the body of `ApplyImportSettings` into an
> `OnPreprocessTexture` hook gated on `assetPath.StartsWith(...)`.

---

## Pixel-Art Philosophy (16 PPU)

Why 16 pixels per unit?

1. **Crisp on portrait mobile.** At a 1080×1920 target running at ~9 units of
   vertical view, the player sprite spans ~32 px on-screen — clearly readable
   without ever looking blurry. Point filtering preserves the grid.
2. **Bloom-friendly.** Hard, near-white highlights paired with saturated bodies
   give Bloom's bright-pass plenty to grab. Anti-aliased edges would muddy the
   threshold and produce a soft, "Vaseline" look.
3. **Fast to iterate.** Small textures encode/decode in microseconds; you can
   tweak `Build*()` and hit a regenerate button without breaking flow.
4. **Future-proof.** When you swap to hand-made art, keeping the same 16 PPU
   contract means colliders/spawn distances/projectile speeds don't need to be
   re-tuned.

Hard rules in the rasterizer:
- Outlines are **1-pixel hard edges** (8-neighbor erode pass).
- Fills are **integer pixel inclusion tests** — no fractional coverage.
- The only "soft" pixels are in FX masks (`ExplosionMask`, `SparkMask`,
  `Projectile`, `UiCircleIcon`'s outer halo) where the alpha gradient IS the
  effect.

---

## Swapping In Real Art

The Generated/ folder is meant to be **disposable**. To replace a placeholder
with real art:

### Option A — drop-in override (recommended)
1. Save your final art as `Assets/Sprites/<SameName>.png` (note: parent folder,
   not `Generated/`). Example: `Assets/Sprites/Player.png`.
2. In your prefabs / ScriptableObjects, update the sprite reference to the new
   path. Because the path differs (`Sprites/` vs `Sprites/Generated/`), the
   placeholder remains available as a fallback during reviews.
3. Re-import with the same pixel-art settings (Sprite, 16 PPU, Point, Clamp,
   alphaIsTransparency, no mips).

### Option B — overwrite in place
1. Drop your PNG into `Assets/Sprites/Generated/` using the exact same filename
   (e.g. `EnemyTank.png`).
2. **Do not** click *Generate All Sprites* afterwards — that would overwrite
   your art. Use the per-piece buttons for everything except the file you
   replaced.
3. The importer settings already applied to the asset will carry over, so it'll
   load with the correct PPU/filter without any extra config.

### Option C — different art, different name
If you want both the placeholder AND the real art in the project (e.g. to A/B):
just name the file something else and update the sprite reference. Nothing in
the tool depends on a specific filename — it only writes to the names listed
in `Generators[]` inside `SpriteGeneratorWindow.cs`.

---

## Recommended CC0 / Free-License Sources (Production)

When you're ready to leave placeholder land, these are battle-tested:

- **Kenney 1-Bit Pack** — <https://kenney.nl/assets/1-bit-pack> — perfect when
  you want to keep the chunky pixel-art read with a unified single-color style.
- **Kenney Tiny Dungeon** — <https://kenney.nl/assets/tiny-dungeon> — 16×16
  tileset + characters, drops in almost 1:1 with our 16 PPU target.
- **kenney.nl (all packs)** — <https://kenney.nl/assets> — CC0 everything,
  always safe.
- **itch.io CC0 tag** — <https://itch.io/game-assets/tag-cc0> — broad selection,
  filter by 2D / pixel art.
- **OpenGameArt** — <https://opengameart.org/> — older but huge; double-check
  per-asset license (CC0 / CC-BY / GPL all exist there).

**License note:** verify each asset's license individually. CC0 is fully
permissive (no attribution required). CC-BY requires you to credit the author —
keep a `CREDITS.md` in the repo if you mix licenses.
