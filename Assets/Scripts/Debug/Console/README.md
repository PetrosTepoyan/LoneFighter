# DevConsole

In-game developer console for fast iteration. Active only in **debug builds** and the **Unity Editor** — completely inert in release builds.

## Toggle

| Platform | Gesture |
| --- | --- |
| Desktop | <kbd>~</kbd> / <kbd>`</kbd> (backquote) via the new Input System |
| Mobile | Tap with **four fingers simultaneously** on the touchscreen |

The console renders as an IMGUI overlay across the **top 40% of the screen** with a dark background, monospace font, scrollable log on top, and a single-line input field at the bottom.

## Files

| File | Purpose |
| --- | --- |
| `DevConsole.cs` | Singleton MonoBehaviour. Auto-bootstraps via `RuntimeInitializeOnLoadMethod` in debug builds. IMGUI overlay + input field + toggle gestures. |
| `ConsoleCommandRegistry.cs` | Static registry. Use `ConsoleCommandRegistry.Register(name, handler, description)` to add new commands. Built-ins are auto-registered on first access. |
| `ConsoleLogCapture.cs` | 200-entry ring buffer attached to `Application.logMessageReceived`. Colour-coded by `LogType`. |
| `ConsoleHistory.cs` | Up/Down arrow history of the last 20 submitted commands. |
| `ConsoleAutoComplete.cs` | Tab-completion. Completes to the longest common prefix of matching command names. |

All types live under the namespace `LoneFighter.Debugging.Console`.

## Built-in commands

| Command | Description |
| --- | --- |
| `help` | Lists every registered command with its description. |
| `clear` | Clears the log buffer. |
| `give_xp <amount>` | Calls `PlayerLevel.AddXp(amount)` on `PlayerController.Instance`. |
| `spawn <enemyName> [count]` | Finds an `EnemySpawner` via `Object.FindFirstObjectByType<EnemySpawner>()`, looks up a matching `EnemyData` (Resources first, AssetDatabase fallback in the Editor) and calls `TrySpawn` `count` times. Name match is case-insensitive against both `displayName` and the asset name. |
| `set_hp <amount>` | Writes `PlayerHealth.Current` via reflection (property/backing field). Falls back to `ApplyDamage` + `Heal` if reflection cannot reach the setter. |
| `god_mode <on\|off>` | Reflects on any sibling `Cheats` / `CheatsService` / `CheatManager` type to call `SetGodMode(bool)`, `ToggleGodMode(bool)`, `GodMode(bool)`, or `SetInvulnerable(bool)`. Falls back to a full-heal if no such system exists yet. |
| `time_scale <0..2>` | Clamps and writes `Time.timeScale`. |
| `next_level` | Forces a level-up by feeding `AddXp` a multiple of `XpToNext`. |
| `kill_all` | Iterates a snapshot of `EnemyRegistry.Enemies` and calls `EnemyHealth.ApplyDamage(99999)` on each. |
| `quit` | `Application.Quit()` (stops play mode in the Editor). |
| `version` | Prints `bundleVersion` + platform build number (Editor) or `Application.version` (player). |

## Adding commands

```csharp
using LoneFighter.Debugging.Console;

// Somewhere during bootstrap or in a static constructor.
ConsoleCommandRegistry.Register(
    "warp",
    args =>
    {
        if (args.Length < 2) return "Usage: warp <x> <y>";
        if (!float.TryParse(args[0], out var x) || !float.TryParse(args[1], out var y))
            return "Invalid coordinates";
        PlayerController.Instance.transform.position = new Vector3(x, y, 0);
        return $"Warped to ({x}, {y})";
    },
    "Usage: warp <x> <y> — teleports the player.");
```

Handlers receive the **argument array** (the command name is stripped) and return a string that gets pushed into the log. Exceptions thrown by handlers are caught and printed as red error lines — your handler will not crash the console.

## Hotkeys (while console is visible)

| Key | Action |
| --- | --- |
| <kbd>Enter</kbd> | Submit the current input. |
| <kbd>Tab</kbd> | Autocomplete to the longest common prefix. Lists matches when ambiguous. |
| <kbd>Up</kbd> / <kbd>Down</kbd> | Walk through command history (last 20). |
| <kbd>Esc</kbd> | Close the console. |
| <kbd>~</kbd> | Close the console (same as toggle). |

## Notes

- The console captures `Application.logMessageReceived` so anything passed to `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` shows up automatically, colour-coded by `LogType`.
- Auto-bootstrap uses `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` so the console is available the moment any scene loads in debug builds. No manual setup required.
- All UI is IMGUI to keep the dependency surface zero — no scene assets, no prefabs, no `Canvas`. Survives scene reloads via `DontDestroyOnLoad`.
- Mobile: the four-finger gesture uses `Touchscreen.current.touches`, counting simultaneous active presses. A latch prevents repeated toggling while the gesture is held.
