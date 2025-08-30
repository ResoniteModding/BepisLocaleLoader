# BepisLocaleLoader
A [Resonite](https://resonite.com/) mod that Loads locale files for Plugins.

## Installation (Manual)
1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader) for Resonite.
2. Download the latest release ZIP file (e.g., `ResoniteModding-BepisLocaleLoader-1.0.0.zip`) from the [Releases](https://github.com/ResoniteModding/BepisLocaleLoader/releases) page.
3. Extract the ZIP and copy the `plugins` folder to your BepInEx folder in your Resonite installation directory:
   - **Default location:** `C:\Program Files (x86)\Steam\steamapps\common\Resonite\BepInEx\`
4. Start the game. If you want to verify that the mod is working you can check your BepInEx logs.

# Usage

---

## Adding Locale Strings Manually

You can register a single localized string in code with:

```csharp
LocaleLoader.AddLocaleString(
    "Settings.BepInEx.Core.Config",
    "Reset Configuration",
    force: true,
    authors: "YourName"
);
```

- `rawString` → The key used to look up the string.
- `localeString` → The localized text to display.
- `force` → If `true`, always overwrites existing entries.
- `authors` → Optional, comma-separated string of authors. Defaults to plugin authors or `"BepInEx"`.

---

## Loading Locale Files from a Plugin

Place a `Locale` folder next to your plugin DLL (e.g. `MyPlugin/Locale/en.json`).  
Files should follow the `LocaleData` JSON structure:

```json
{
  "localeCode": "en-US",
  "authors": ["MyName"],
  "messages": {
    "Settings.MyPlugin.Option": "Enable Feature",
    "Settings.MyPlugin.Description": "This feature toggles something cool."
  }
}
```

Load all locale files from a plugin with:

```csharp
LocaleLoader.AddLocaleFromPlugin(myPluginInfo);
```

Or load a single JSON file manually:

```csharp
LocaleLoader.AddLocaleFromFile("path/to/Locale/en.json");
```

---

## String Shorthand: `.T()`

To make localization calls cleaner, `LocaleLoader` defines extension methods `.T()` that wrap `AsLocaleKey` and `LocaleString`.

Examples:

```csharp
// Basic key lookup
"Settings.MyPlugin.Option".T();

// Key with formatting arguments
"Settings.MyPlugin.Welcome".T("Hello {name}!", "name", userName);

// Multiple arguments
"Settings.MyPlugin.Stats".T(
    ("kills", killCount),
    ("deaths", deathCount)
);

// With format string
"Settings.MyPlugin.Formatted".T("{kills}/{deaths}", 
    ("kills", killCount), 
    ("deaths", deathCount)
);

// Continuous locale updates (useful for dynamic UI)
"Settings.MyPlugin.Timer".T(
    continuous: true, 
    arguments: new Dictionary<string, object> { { "time", elapsedTime } }
);
```

---

## When to Use `force: true`

- Use `force: false` (default) when you want to avoid overwriting existing locales (safe for mods adding only new keys).
- Use `force: true` when your plugin should override existing translations (e.g. testing, fixes, or ensuring your text is applied).

---

## Debugging

- Loaded plugin locales are tracked in `LocaleLoader.PluginsWithLocales`.
- Logs show which files are loaded and how many messages were registered.
- Errors during JSON parsing will be logged with details.  