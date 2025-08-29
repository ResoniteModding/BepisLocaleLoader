using System.Text.Json;
using BepInEx;
using Elements.Assets;
using FrooxEngine;

namespace BepisLocaleLoader;

// Edited Locale Code is from - https://github.com/Xlinka/Project-Obsidian/blob/main/ProjectObsidian/Settings/LocaleHelper.cs
public static class LocaleLoader
{
    private static StaticLocaleProvider _localeProvider;
    private static string _lastOverrideLocale;
    private const string OverrideLocaleString = "somethingRandomJustToMakeItChange";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static readonly HashSet<PluginInfo> PluginsWithLocales = new HashSet<PluginInfo>();

    public static void AddLocaleString(string rawString, string localeString, bool force = false, string authors = null)
    {
        List<string> finalAuthors;

        if (!string.IsNullOrWhiteSpace(authors))
        {
            finalAuthors = authors.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(PluginMetadata.AUTHORS))
        {
            finalAuthors = PluginMetadata.AUTHORS.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else
        {
            finalAuthors = new List<string> { "BepInEx" };
        }

        LocaleData localeData = new LocaleData
        {
            LocaleCode = "en-US",
            Authors = finalAuthors,
            Messages = new Dictionary<string, string>
            {
                { rawString, localeString }
            }
        };

        Update(localeData, force);
    }

    public static void AddLocaleFromPlugin(PluginInfo plugin)
    {
        string dir = Path.GetDirectoryName(plugin.Location);
        string locale = Path.Combine(dir, "Locale");

        if (!Path.Exists(locale)) return;

        Plugin.Log.LogDebug($"Adding locale for {plugin.Metadata.GUID}");
        ProcessPath(locale, AddLocaleFromFile);

        PluginsWithLocales.Add(plugin);
    }

    public static void AddLocaleFromFile(string path)
    {
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);
        LocaleData localeData;
        try
        {
            localeData = JsonSerializer.Deserialize<LocaleData>(json, JsonOptions);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError(e);
            return;
        }

        Plugin.Log.LogDebug($"- {localeData.LocaleCode} - c-{localeData.Messages.Count}");

        Update(localeData, true);
    }

    private static void Update(LocaleData localeData, bool force)
    {
        UpdateDelayed(localeData, force);
        Settings.RegisterValueChanges<LocaleSettings>(_ => UpdateDelayed(localeData, force));
    }

    private static void UpdateDelayed(LocaleData localeData, bool force)
    {
        Userspace.UserspaceWorld?.RunInUpdates(15, () => UpdateIntern(localeData, force));
    }

    private static void UpdateIntern(LocaleData localeData, bool force)
    {
        _localeProvider = Userspace.UserspaceWorld?.GetCoreLocale();
        if (_localeProvider?.Asset?.Data is null)
        {
            Userspace.UserspaceWorld?.RunSynchronously(() => UpdateIntern(localeData, force));
        }
        else
        {
            UpdateLocale(localeData, force);
        }
    }

    private static void UpdateLocale(LocaleData localeData, bool force)
    {
        if (_localeProvider?.Asset?.Data != null)
        {
            if (!force)
            {
                string firstKey = localeData.Messages.Keys.FirstOrDefault();

                bool alreadyExists = _localeProvider.Asset.Data.Messages.Any(ld => ld.Key == firstKey);
                if (alreadyExists) return;
            }

            _localeProvider.Asset.Data.LoadDataAdditively(localeData);

            // force asset update for locale provider
            if (_localeProvider.OverrideLocale.Value != null && _localeProvider.OverrideLocale.Value != OverrideLocaleString)
            {
                _lastOverrideLocale = _localeProvider.OverrideLocale.Value;
            }

            _localeProvider.OverrideLocale.Value = OverrideLocaleString;
            Userspace.UserspaceWorld.RunInUpdates(1, () => { _localeProvider.OverrideLocale.Value = _lastOverrideLocale; });
        }
        else if (_localeProvider?.Asset?.Data == null)
        {
            Plugin.Log.LogError("Locale data is null when it shouldn't be!");
        }
    }

    private static void ProcessPath(string path, Action<string> fileAction)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException("Directory not found: " + path);
        }

        ProcessDirectory(path, fileAction);
    }

    private static void ProcessDirectory(string directory, Action<string> fileAction)
    {
        string[] files = Directory.GetFiles(directory);
        foreach (string file in files)
        {
            fileAction(file);
        }

        string[] subdirectories = Directory.GetDirectories(directory);
        foreach (string subdir in subdirectories)
        {
            ProcessDirectory(subdir, fileAction);
        }
    }
}