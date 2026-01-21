using System.Text.Json;
using BepInEx;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;

namespace BepisLocaleLoader;

/// <summary>
/// Public API for locale loading.
/// Primary locale injection happens via Harmony patch in LocaleInjectionPatch.
/// This class provides runtime APIs for adding locales after initial load.
/// </summary>
public static class LocaleLoader
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly object _pluginsLock = new();

    /// <summary>
    /// Tracks which plugins have locale files.
    /// </summary>
    public static readonly HashSet<PluginInfo> PluginsWithLocales = new();

    /// <summary>
    /// Thread-safe method to add a plugin to the tracking set.
    /// </summary>
    internal static void TrackPluginWithLocale(PluginInfo plugin)
    {
        lock (_pluginsLock)
        {
            PluginsWithLocales.Add(plugin);
        }
    }

    /// <summary>
    /// Add a single locale string at runtime.
    /// For bulk loading, use the Locale/ folder in your plugin directory.
    /// </summary>
    public static void AddLocaleString(string rawString, string localeString, bool force = false, string? authors = null)
    {
        List<string> finalAuthors;

        if (!string.IsNullOrWhiteSpace(authors))
        {
            finalAuthors = authors.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(Plugin.AUTHORS))
        {
            finalAuthors = Plugin.AUTHORS.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else
        {
            finalAuthors = ["BepInEx"];
        }

        LocaleData localeData = new()
        {
            LocaleCode = "en-US",
            Authors = finalAuthors,
            Messages = new Dictionary<string, string>
            {
                { rawString, localeString }
            }
        };

        InjectLocaleData(localeData, force);
    }

    /// <summary>
    /// Add locales from a plugin's Locale/ folder at runtime.
    /// Note: This is automatically handled by the Harmony patch during locale loading.
    /// </summary>
    public static void AddLocaleFromPlugin(PluginInfo plugin)
    {
        var localeFiles = GetPluginLocaleFiles(plugin).ToList();
        if (localeFiles.Count == 0) return;

        Plugin.Log.LogDebug($"Adding locale for {plugin.Metadata?.GUID ?? "unknown"}");

        foreach (string file in localeFiles)
        {
            AddLocaleFromFile(file);
        }

        TrackPluginWithLocale(plugin);
    }

    /// <summary>
    /// Gets all locale JSON files from a plugin's Locale/ folder.
    /// </summary>
    internal static IEnumerable<string> GetPluginLocaleFiles(PluginInfo plugin)
    {
        string? pluginDir = Path.GetDirectoryName(plugin.Location);
        if (string.IsNullOrEmpty(pluginDir))
            return [];

        string localeDir = Path.Combine(pluginDir, "Locale");

        if (!Directory.Exists(localeDir))
            return [];

        return Directory.GetFiles(localeDir, "*.json", SearchOption.AllDirectories);
    }

    /// <summary>
    /// Add locale from a specific file at runtime.
    /// </summary>
    public static void AddLocaleFromFile(string path)
    {
        var localeData = LoadLocaleDataFromFile(path);
        if (localeData == null) return;

        Plugin.Log.LogDebug($"- LocaleCode: {localeData.LocaleCode}, Message Count: {localeData.Messages.Count}");

        InjectLocaleData(localeData, force: true);
    }

    /// <summary>
    /// Loads and parses a locale JSON file, returning the LocaleData or null on failure.
    /// </summary>
    internal static LocaleData? LoadLocaleDataFromFile(string path)
    {
        if (!File.Exists(path)) return null;

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error reading locale file {path}: {e}");
            return null;
        }

        LocaleData? localeData;
        try
        {
            localeData = JsonSerializer.Deserialize<LocaleData>(json, JsonOptions);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error parsing locale file {path}: {e}");
            return null;
        }

        if (localeData?.Messages == null)
        {
            Plugin.Log.LogError($"Invalid locale file (missing messages): {path}");
            return null;
        }

        return localeData;
    }

    /// <summary>
    /// Inject locale data into the current locale provider.
    /// </summary>
    private static void InjectLocaleData(LocaleData localeData, bool force)
    {
        var localeProvider = Userspace.UserspaceWorld?.GetCoreLocale();

        if (localeProvider?.Asset?.Data == null)
        {
            Plugin.Log.LogWarning("Cannot inject locale data - locale provider not available yet");
            return;
        }

        if (!force)
        {
            string? firstKey = localeData.Messages.Keys.FirstOrDefault();
            if (firstKey != null)
            {
                bool alreadyExists = localeProvider.Asset.Data.Messages.Any(ld => ld.Key == firstKey);
                if (alreadyExists) return;
            }
        }

        localeProvider.Asset.Data.LoadDataAdditively(localeData);
    }

    #region String Formatting Extensions

    private static string GetFormattedLocaleString(this string key, string? format, Dictionary<string, object>? dict, (string, object)[]? arguments)
    {
        var localeProvider = Userspace.UserspaceWorld?.GetCoreLocale();

        Dictionary<string, object> merged = dict != null ? new Dictionary<string, object>(dict) : new Dictionary<string, object>();

        if (arguments != null)
        {
            foreach ((string name, object value) in arguments)
            {
                merged[name] = value;
            }
        }

        string? formatted = localeProvider?.Asset?.Format(key, merged);

        if (!string.IsNullOrWhiteSpace(format))
        {
            formatted = string.Format(format, formatted);
        }

        return formatted ?? key;
    }

    public static string GetFormattedLocaleString(this string key) => key.GetFormattedLocaleString(null, null, null);
    public static string GetFormattedLocaleString(this string key, string format) => key.GetFormattedLocaleString(format, null, null);
    public static string GetFormattedLocaleString(this string key, string argName, object argField) => key.GetFormattedLocaleString(null, null, [(argName, argField)]);
    public static string GetFormattedLocaleString(this string key, string format, string argName, object argField) => key.GetFormattedLocaleString(format, null, [(argName, argField)]);
    public static string GetFormattedLocaleString(this string key, params (string, object)[] arguments) => key.GetFormattedLocaleString(null, null, arguments);
    public static string GetFormattedLocaleString(this string key, string format, params (string, object)[] arguments) => key.GetFormattedLocaleString(format, null, arguments);

    #endregion

    #region LocaleString Extensions

    public static LocaleString T(this string str, string argName, object argField) => str.AsLocaleKey(null, (argName, argField));
    public static LocaleString T(this string str, string format, string argName, object argField) => str.AsLocaleKey(format, (argName, argField));
    public static LocaleString T(this string str, params (string, object)[] arguments) => str.AsLocaleKey(null, arguments);
    public static LocaleString T(this string str, string format, params (string, object)[] arguments) => str.AsLocaleKey(format, arguments);
    public static LocaleString T(this string str, bool continuous, Dictionary<string, object>? arguments = null) => new(str, null, true, continuous, arguments);
    public static LocaleString T(this string str, string? format = null, bool continuous = true, Dictionary<string, object>? arguments = null) => new(str, format, true, continuous, arguments);

    #endregion
}
