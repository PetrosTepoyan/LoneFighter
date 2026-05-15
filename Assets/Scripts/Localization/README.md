# Localization

Lightweight, allocation-free, ScriptableObject-based localization for LoneFighter. English is the shipped baseline; other locales are added by duplicating the English asset.

## Files

- `LocalizationData.cs` — `ScriptableObject` holding one locale's key/value table as **parallel string arrays** (JsonUtility-safe, version-control friendly).
- `LocaleRegistry.cs` — `ScriptableObject` listing every locale shipped with the game. Case-insensitive `Get(code)` with a language-prefix fallback (e.g. `pt-br` → `pt`).
- `LocalizationService.cs` — runtime singleton. Loads the registry from `Resources/Localization/LocaleRegistry`. Picks the locale based on (1) `PlayerPrefs["LF.Locale"]`, (2) `Application.systemLanguage`, (3) the registry's first non-null locale. Public API:
  - `string Get(string key, string fallback = null)`
  - `void SetLocale(string code)`
  - `event Action OnLocaleChanged`
  - `string CurrentLocaleCode`, `LocalizationData CurrentLocale`, `LocaleRegistry Registry`
- `LocalizedText.cs` — `MonoBehaviour`. Drop onto any GameObject with a `TMP_Text`. Set the inspector field `localizationKey` (e.g. `ui.play`). The component refreshes on `OnEnable` and on every `OnLocaleChanged`.
- `LocalizationKeys.cs` — every canonical key as `public const string`. Compile-time-checked references for gameplay/UI code.
- `Editor/LocalizationGenerator.cs` — editor utility that regenerates the English baseline. Menu: **LoneFighter → Localization → Generate English Baseline**.

## Bootstrap

`LocalizationService` self-bootstraps via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`. No GameObject needed. The very first `LocalizedText.OnEnable` will already see the active locale.

## First-time setup (one-time, in Editor)

1. From the menu bar: **LoneFighter → Localization → Generate English Baseline**.
2. This creates:
   - `Assets/Data/Localization/English.asset` — populated with one row per key in `LocalizationKeys`, with sensible default English strings.
   - `Assets/Resources/Localization/LocaleRegistry.asset` — referencing the English asset. Lives under `Resources/` so the service can `Resources.Load` it without scene wiring.
3. (Optional) Open `English.asset` in the inspector and tweak any strings — re-running the generator preserves your edits and only adds/removes keys.

## Using a localized string in code

```csharp
using LoneFighter.Localization;

string label = LocalizationService.Instance.Get(LocalizationKeys.GameoverVictory, "Victory!");
```

The second argument is a hard-coded fallback used if the key is missing from every locale.

## Using a localized string in the UI

1. Select the TMP_Text GameObject (e.g. the "Play" button label).
2. **Add Component → LoneFighter → Localization → Localized Text**.
3. Type the key — e.g. `ui.play` — in the **Localization Key** field. Copy/paste from `LocalizationKeys.cs` to avoid typos.
4. Leave **Fallback** empty to use the existing authoring-time TMP text as the fallback. Otherwise type a fallback string.

The `LocalizedText` component is **additive**: it never modifies the TMP_Text component's other properties (font, color, alignment, etc.), so it's safe to retrofit onto existing UI prefabs.

## Adding a new language

> No code changes required.

1. **Duplicate the English asset**:
   - In the Project window, navigate to `Assets/Data/Localization/`.
   - Right-click `English.asset` → **Duplicate**.
   - Rename the duplicate, e.g. `Spanish.asset`.
2. **Change `localeCode`**: open the new asset and set `localeCode` to the ISO-639-1 code (`es`, `fr`, `de`, `pt-br`, `zh-hans`, ...). Set `displayName` to the user-facing name (`Espanol`).
3. **Translate the `values` array**: leave the `keys` array untouched. The N-th value is the translation of the N-th key. The order matches `LocalizationKeys.cs`.
4. **Register the new locale**:
   - Open `Assets/Resources/Localization/LocaleRegistry.asset`.
   - Drag your new `.asset` into the **Locales** array. Order does not matter except that the **first** locale is treated as the fallback when a key is missing — keep English first.
   - Or simply re-run **LoneFighter → Localization → Generate English Baseline** — the generator auto-scans `Assets/Data/Localization/` and rebuilds the registry's locale list. It will preserve any existing English values.
5. **Test**: enter Play mode and either:
   - Set the device/Editor system language to the new locale, or
   - Call `LocalizationService.Instance.SetLocale("es")` from a debug button.

### What about keys that the new locale doesn't translate?

`LocalizationService.Get` falls back, in order: active locale → registry's first locale (English) → the explicit `fallback` argument → the key string itself. So missing translations always show *something*, never `null`.

### Adding a new key

1. Add a `public const string` in `LocalizationKeys.cs`.
2. (Optional, recommended) add the default English string in `LocalizationGenerator.DefaultsTable()`. Without it the generator falls back to a humanized version of the key suffix (e.g. `ui.new_thing` → `"New Thing"`).
3. Run **LoneFighter → Localization → Generate English Baseline**. The new key is appended to `English.asset` (and any extra locales — those will need manual translation; their existing values are untouched).

## Conventions

- Keys are lowercase, dot-separated, namespaced by surface: `ui.*`, `hud.*`, `levelup.*`, `gameover.*`, `settings.*`, `unlock.*`, `tutorial.*`, `weapon.*`, `enemy.*`, `pickup.*`, `pause.*`, `stats.*`.
- A few keys use `{0}` placeholders for `string.Format` (e.g. `hud.level = "Lv {0}"`). Locales must preserve the placeholders — translation tools/spreadsheets should mark these as DO-NOT-TRANSLATE.
- Do not rename a key without bumping every locale asset; locales store the key as a string, not as a const reference. Removing a key is safe (the generator strips stale keys from English on regeneration, but other locales need manual cleanup).

## Performance

- On locale load, every key/value pair is copied once into a `Dictionary<string,string>` for O(1) lookup.
- `LocalizedText.Apply` does one dictionary lookup + one TMP_Text string assignment — zero per-frame cost when idle (it only runs on `OnEnable` and `OnLocaleChanged`).
- The registry asset and locale assets total ~kB — negligible vs. typical Unity asset sizes.
