# LoneFighter Diagnostics Overlays

Advanced runtime diagnostic overlays that layer on top of the existing F3 FPS
counter (`Assets/Scripts/Polish/PerformanceMonitor.cs`).

All overlays live under the namespace `LoneFighter.Debugging.Diagnostics` and
are gated behind `Debug.isDebugBuild || Application.isEditor`. In a Release
build with `Development Build` unchecked, every overlay early-outs in its first
guard and contributes no per-frame cost.

## Hotkeys

| Key | Overlay                  | Description                                                  |
|-----|--------------------------|--------------------------------------------------------------|
| F3  | `PerformanceMonitor`     | (existing) FPS / ms / enemy count / transient count          |
| F4  | `PoolDiagnosticsOverlay` | Per-pool active / free counts via reflection on PoolService  |
| F5  | `EnemyAiVisualizer`      | Chase lines, dash arrows, shockwave rings                    |
| F6  | `MemoryOverlay`          | Managed MB, GC generation counts, allocation rate            |
| F7  | `FrameTimeGraph`         | 120-frame rolling chart, green/yellow/red tiers              |
| F8  | `BuildInfoPanel`         | Toggle the always-on build info panel                        |

## Architecture

```
DiagnosticsHub  (singleton, auto-spawned BeforeSceneLoad)
   ├── PoolDiagnosticsOverlay   (F4)
   ├── EnemyAiVisualizer        (F5)
   ├── MemoryOverlay            (F6)
   ├── FrameTimeGraph           (F7)
   └── BuildInfoPanel           (F8 – defaults on)

DiagnosticsSettings  (PlayerPrefs-backed bool toggles)
```

`DiagnosticsHub` is bootstrapped via `[RuntimeInitializeOnLoadMethod]` so no
scene wiring is required. It listens for F4..F8 using the new Input System
(`Keyboard.current`) and flips the corresponding `DiagnosticsSettings` flag,
which each sub-overlay re-reads in its render loop.

## Defaults

Per spec, all overlays default **on in the Editor** and **off in Release**.
Once a toggle is hit by the user, the choice is persisted in `PlayerPrefs`
(keys prefixed `LF.Diag.*`) and used for every future launch.

## Reflection notes

`PoolDiagnosticsOverlay` reads `PoolService.Instance._pools` (a private
`Dictionary<GameObject, ObjectPool<GameObject>>`) via reflection. For each
entry it pulls the public `CountActive` and `CountInactive` properties off
`UnityEngine.Pool.ObjectPool<GameObject>`. If reflection fails (PoolService
moved namespace, field renamed) it falls back to showing
`PoolService.Instance.gameObject.transform.childCount` as a rough proxy.

`EnemyAiVisualizer` reflects:
- `LoneFighter.Enemies.EnemyRegistry.Enemies`
- `LoneFighter.Player.PlayerController.Instance`
- `LoneFighter.Enemies.DasherAI._dashDirection`
- `LoneFighter.Enemies.EnemyShockwave._elapsed / expandDuration / maxRadius`

It renders via `OnDrawGizmos` (Scene view in Editor) **and** a pooled
`LineRenderer` path for runtime builds. The line material uses the
`Sprites/Default` shader, which is URP 2D compatible and present in every
URP project.

## Files

- `DiagnosticsHub.cs` – singleton, hotkey dispatch, sub-overlay composition
- `DiagnosticsSettings.cs` – PlayerPrefs-backed toggle accessors
- `PoolDiagnosticsOverlay.cs` – F4 pool listing
- `EnemyAiVisualizer.cs` – F5 chase / dash / shockwave gizmos
- `MemoryOverlay.cs` – F6 managed memory + GC counters
- `FrameTimeGraph.cs` – F7 120-frame chart
- `BuildInfoPanel.cs` – F8 build identification panel

## Adding a new overlay

1. Add a new `MonoBehaviour` to this folder under the same namespace.
2. Have it early-out on `!DiagnosticsSettings.DiagnosticsAllowed`.
3. Add a `PlayerPrefs`-backed toggle accessor in `DiagnosticsSettings.cs`.
4. Add a `GetOrAdd<T>()` line in `DiagnosticsHub.EnsureOverlays` and a hotkey
   block in `DiagnosticsHub.Update`.
