# Run Summary UI

Rich end-of-run summary panel: title (VICTORY / DEFEATED), survived time, kills, peak level, XP earned, MVP weapon, per-enemy kill counts, taken upgrades.

## Files

- `RunSummaryData.cs` — plain C# struct with all summary fields. `RunSummaryData.Build()` snapshots a best-effort summary from `GameManager.Instance` + `PlayerController.Instance.GetComponent<PlayerLevel>()`. Fields not yet tracked default to empty/zero.
- `RunSummaryPanel.cs` — `MonoBehaviour` UI controller. On `OnEnable` it builds a `RunSummaryData` and populates the panel; numeric fields count up over `countUpSeconds` seconds (default 0.6s) using unscaled time so it works while the game is paused.
- `CountUpText.cs` — small utility that animates an integer up over time on a `TMP_Text`. Cubic ease-out (`1 - (1-t)^3`) for a satisfying tick-up. Drop onto any TMP_Text whose number should animate.

## Wiring into the GameOver scene

The existing `GameOverController.cs` (one-line summary + Retry / Menu buttons) already lives in the `GameOver` scene and is loaded by `GameManager.TriggerGameOver()` / `TriggerVictory()`. **Do not modify it.** Instead, add a `RunSummaryPanel` next to it; both can coexist on the same scene.

### Recommended approach

1. Open `Assets/Scenes/GameOver.unity`.
2. Replace (or augment) the existing summary text area with a richer panel that has the controls listed below.
3. Add the `RunSummaryPanel` component to the panel root and drag in references.
4. Make sure your Retry / Menu buttons in the rich panel are wired to `RunSummaryPanel.retryButton` / `menuButton` (the panel hooks them itself — don't add a second OnClick).
5. (Optional) Hide or remove the legacy `GameOverController` text once the rich panel is satisfying. The legacy script can stay on the scene as a fallback.

### Required `RunSummaryPanel` references

| Field | Type | Notes |
|---|---|---|
| `titleText` | `TMP_Text` | Shows `VICTORY!` or `DEFEATED`. Labels are inspector-configurable. |
| `survivedText` | `TMP_Text` | Formatted as `MM:SS` via `GameManager.FormatTime`. Animates up. |
| `killsText` | `TMP_Text` | Animates up. Add a `CountUpText` component for the easing curve. |
| `peakLevelText` | `TMP_Text` | Animates up. Add a `CountUpText` component. |
| `xpEarnedText` | `TMP_Text` | Animates up. Add a `CountUpText` component. |
| `mvpWeaponText` | `TMP_Text` | Shows weapon display name, or `—` if not tracked yet. |
| `killsByEnemyRoot` | `Transform` | Parent of generated rows. Pair with a `VerticalLayoutGroup`. |
| `killsByEnemyRowPrefab` | `GameObject` | Must contain a child `TMP_Text`. One instantiated per (enemy, count) pair. |
| `upgradesRoot` | `Transform` | Parent of generated rows. Pair with a `VerticalLayoutGroup`. |
| `upgradeRowPrefab` | `GameObject` | Must contain a child `TMP_Text`. One instantiated per upgrade pick. |
| `retryButton` | `Button` | Calls `GameManager.LoadGameScene()`. |
| `menuButton` | `Button` | Calls `GameManager.LoadMainMenu()`. |
| `countUpSeconds` | `float` | Defaults to `0.6f`. Set `0` to disable animation. |

### Animation set-up (optional but recommended)

Add a `CountUpText` component to each numeric `TMP_Text` (kills, peak level, XP). With a `CountUpText` attached, `RunSummaryPanel` will use its ease-out animation; without one, it'll just set the text value directly. The survived-time field doesn't need `CountUpText` — `RunSummaryPanel` runs its own time-formatted coroutine.

## What's wired today vs. future tasks

`RunSummaryData.Build()` currently fills in:

- `victory` (from `GameManager.State == Victory`)
- `survivedSeconds` (from `GameManager.ElapsedSeconds`)
- `kills` (from `GameManager.Kills`)
- `peakLevel` (from `PlayerLevel.Level`, when the player still exists)
- `xpEarned` (from `PlayerLevel.CurrentXp`)

These default to empty/zero and are listed for future wiring:

- `mvpWeapon` — needs damage-by-weapon tracking inside `WeaponBase` / `Projectile` and a max-damage post-run query.
- `killsByEnemy` — needs `EnemyHealth` to call back into a registry with the enemy display name when the enemy dies.
- `upgradesPicked` — needs `UpgradeService.RecordChoice` to append to a public list.

The panel renders correctly whether or not these fields are populated; the per-enemy and upgrades sections simply stay empty until the data exists. None of that wiring is in this PR.
