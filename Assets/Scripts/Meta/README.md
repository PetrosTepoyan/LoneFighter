# Meta-progression & persistence (`LoneFighter.Meta`)

This folder owns everything that survives a single run: the on-disk save file, the in-memory
cache, run history, all-time stats, weapon/achievement unlocks, and the Main-Menu UI that
displays them.

Files:

- `SaveService.cs` — static, owns disk I/O.
- `SaveData.cs` — `[Serializable]` POCOs (`SaveData`, `PlayerProfile`, `RunRecord`,
  `AllTimeStats`, `Settings`).
- `UnlockManager.cs` — static rule table + `Evaluate(...)`.
- `MetaProgression.cs` — singleton `MonoBehaviour`, subscribes to game events.
- `UnlockToast.cs` — UI: slide-down notification panel.
- `StatsPanel.cs` — UI: read-only all-time stats display.

Nothing outside this folder is modified. All hooks into the rest of the codebase are
**subscriptions** to existing public events on `GameManager`.

---

## Save file location

`SaveService.SavePath` resolves to `Application.persistentDataPath + "/savegame.json"`.

| Platform | Path                                                                    |
| -------- | ----------------------------------------------------------------------- |
| Windows  | `%userprofile%\AppData\LocalLow\<company>\<product>\savegame.json`      |
| macOS    | `~/Library/Application Support/<company>/<product>/savegame.json`       |
| Linux    | `~/.config/unity3d/<company>/<product>/savegame.json`                   |
| Android  | `/storage/emulated/0/Android/data/<package>/files/savegame.json`        |
| iOS     | `<app sandbox>/Documents/savegame.json`                                  |
| Editor   | Same as Windows/macOS/Linux for the editor user.                         |

Sibling files used during write:

- `savegame.json.tmp` — staging file. Written first; renamed via `File.Replace` for atomicity.
- `savegame.json.bak` — produced by `File.Replace` as the previous-version backup.

`Wipe()` removes all three.

---

## Schema versioning

`SaveData.saveVersion` is `1` today. Migration policy when it changes:

1. Bump `saveVersion` to the next integer in `SaveData.cs`.
2. In `SaveService.Reload()`, after `JsonUtility.FromJson<SaveData>(...)` and **before**
   `EnsureDefaults()`, branch on `data.saveVersion` and run a per-version migration step
   (e.g. `if (data.saveVersion < 2) MigrateV1ToV2(data);`). Migration steps mutate `data` in
   place, finally setting `data.saveVersion = 2`.
3. Migrations must be **additive and defensive**: never throw on missing data, never delete
   user-earned unlocks. If a field disappears, ignore the legacy value rather than failing.
4. `EnsureDefaults()` is the safety net for missing/null sub-objects across all versions —
   keep extending it when new sub-objects are added.

We never store dictionaries — `JsonUtility` cannot serialize them. Keep everything in
`List<T>` form with stable string keys.

---

## Wiring in the Editor

### `MetaProgression`

1. In your bootstrap scene (or the `MainMenu` scene that loads first), create an empty
   GameObject called **`MetaProgression`**.
2. Add the `MetaProgression` component. No fields to wire — it self-subscribes to
   `GameManager.Instance` events and survives scene loads via `DontDestroyOnLoad`.
3. It is safe to have at most one — duplicates self-destruct in `Awake`.

### `UnlockToast`

1. Build a UI canvas overlay (or reuse the HUD's canvas) with a child **Panel** rect
   anchored top-center, sitting just off the top edge of the screen.
2. Add two `TextMeshProUGUI` children: one for the title, one for the description.
3. Drop the `UnlockToast` component onto the canvas root (or any persistent GO) and wire:
   - **panel** -> the Panel `RectTransform`.
   - **titleText** -> the title TMP.
   - **descriptionText** -> the description TMP.
   - **offscreenOffsetY** -> the positive Y distance to slide up by when hidden
     (try `200` for a 200-px-tall toast).
   - **slideDuration** / **visibleDuration** -> tween + dwell timings.

The toast finds `MetaProgression.Instance` on `OnEnable` and subscribes to `OnNewUnlock`. If
your menu scene is loaded before `MetaProgression` exists, the `Start()` re-subscribe handles
the race.

### `StatsPanel`

1. On the Main Menu scene, build a panel containing six TMP fields (runs, victories, kills,
   longest survival, total play time, best kill streak).
2. Drop `StatsPanel` onto the panel root and wire each TMP field plus optional format strings
   (defaults are sensible).
3. Stats refresh in `OnEnable`. If the panel is always-on, call `Refresh()` manually after
   returning from a run.

---

## How other systems should integrate

External gameplay systems (notably `WeaponInventory` and `UpgradeService`) feed the per-run
tracker by calling these methods on `MetaProgression.Instance`:

```csharp
MetaProgression.Instance?.RecordWeaponCarried("Pistol");
MetaProgression.Instance?.RecordUpgradePicked("Bigger Bullets");
MetaProgression.Instance?.SetPeakLevel(playerLevel.Level);
MetaProgression.Instance?.SetDeathCause("Tank contact"); // optional, before TriggerGameOver
```

All four are null-safe and clamp internally. Call them at the moment the event happens
(weapon added to inventory, upgrade chosen, level reached, lethal damage dealt). No
explicit "end of run" call is required — `MetaProgression` listens to
`GameManager.OnStateChanged` and finalizes when state transitions to `GameOver` or
`Victory`.

To check if a piece of content is unlocked from gameplay code (e.g. to filter the level-up
upgrade pool):

```csharp
bool hasShotgun = SaveService.Current.unlockedWeapons.Contains("Spread Shotgun");
```

Note: `UnlockManager` writes weapon ids without spaces (e.g. `SpreadShotgun`). The strings
stored in `SaveData.unlockedWeapons` are the spaceless ids; if you key off the human-readable
weapon name from `ContentSeed.Weapons`, normalize on lookup or update the rule strings to
match the seed's `name` field — pick one and stick with it.

To force a save outside of a run-end (e.g. after the player edits Settings):

```csharp
SaveService.Save();
```

To nuke the save (debug menu / reset-progress button):

```csharp
SaveService.Wipe();
```
