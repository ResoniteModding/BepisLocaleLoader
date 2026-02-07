using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BepInEx.NET.Common;
using Elements.Assets;
using FrooxEngine;
using HarmonyLib;

namespace BepisLocaleLoader;

/// <summary>
/// Harmony patch that injects mod locales immediately after Resonite loads base locale files.
/// This eliminates race conditions by hooking directly into the locale loading flow.
/// </summary>
[HarmonyPatch(typeof(FrooxEngine.LocaleResource), "LoadTargetVariant")]
internal static class LocaleInjectionPatch
{
    private static readonly object _injectionLock = new();
    private static string _lastInjectedLocale = string.Empty;
    private static DateTime _lastInjectionTime = DateTime.MinValue;
    private static readonly TimeSpan _deduplicationWindow = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Postfix that runs after LoadTargetVariant completes.
    /// Waits for the async method to finish, then injects all mod locales.
    /// </summary>
    [HarmonyPostfix]
    private static async void Postfix(FrooxEngine.LocaleResource __instance, Task __result, LocaleVariantDescriptor? variant)
    {
        try
        {
            await __result.ConfigureAwait(false);

            if (__instance.Data == null)
            {
                Plugin.Log.LogWarning("LoadTargetVariant completed but Data is null - skipping locale injection");
                return;
            }

            string targetLocale = variant?.LocaleCode ?? "en";

            // Skip injection for temporary refresh triggers (RML uses "-" to force locale reload)
            if (targetLocale == "-")
            {
                Plugin.Log.LogDebug("Skipping locale injection for refresh trigger (target: -)");
                return;
            }

            lock (_injectionLock)
            {
                var now = DateTime.UtcNow;
                if (_lastInjectedLocale == targetLocale && (now - _lastInjectionTime) < _deduplicationWindow)
                {
                    Plugin.Log.LogDebug($"Skipping duplicate injection for {targetLocale} (called {(now - _lastInjectionTime).TotalMilliseconds:F0}ms after previous)");
                    return;
                }

                Plugin.Log.LogDebug($"Injecting mod locales after LoadTargetVariant completed (target: {targetLocale})");

                _lastInjectedLocale = targetLocale;
                _lastInjectionTime = now;

                InjectAllPluginLocales(__instance.Data, targetLocale);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to inject mod locales: {ex}");
        }
    }

    /// <summary>
    /// Discovers and injects locale files from all BepInEx plugins.
    /// </summary>
    private static void InjectAllPluginLocales(Elements.Assets.LocaleResource localeData, string targetLocale)
    {
        if (NetChainloader.Instance?.Plugins == null || NetChainloader.Instance.Plugins.Count == 0)
        {
            Plugin.Log.LogDebug("No BepInEx plugins loaded - skipping locale injection");
            return;
        }

        int pluginCount = 0;
        int messageCount = 0;

        foreach (var plugin in NetChainloader.Instance.Plugins.Values)
        {
            var localeFiles = LocaleLoader.GetPluginLocaleFiles(plugin).ToList();
            if (localeFiles.Count == 0)
                continue;

            Plugin.Log.LogDebug($"Loading locales from {plugin.Metadata?.GUID ?? "unknown"}");

            var candidates = LoadLocaleFiles(localeFiles);
            var toInject = SelectMatchingLocales(candidates, targetLocale, out bool usingFallback);

            messageCount += InjectAndLogLocales(localeData, toInject, usingFallback);

            LocaleLoader.TrackPluginWithLocale(plugin);
            pluginCount++;
        }

        if (pluginCount > 0)
        {
            Plugin.Log.LogInfo($"Injected {messageCount} locale messages from {pluginCount} plugins");
        }
    }

    /// <summary>
    /// Loads locale data from the specified list of locale files.
    /// </summary>
    private static List<(string Path, LocaleData Data)> LoadLocaleFiles(List<string> localeFiles)
    {
        var candidates = new List<(string Path, LocaleData Data)>();

        foreach (string file in localeFiles)
        {
            var data = LocaleLoader.LoadLocaleDataFromFile(file);
            if (data != null)
            {
                candidates.Add((file, data));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Selects locale data that matches the target locale, with fallback to English if no matches are found.
    /// </summary>
    private static List<(string Path, LocaleData Data)> SelectMatchingLocales(
        List<(string Path, LocaleData Data)> candidates,
        string targetLocale,
        out bool usingFallback)
    {
        var matches = candidates.Where(c => IsLocaleMatch(c.Data.LocaleCode, targetLocale)).ToList();

        usingFallback = false;
        if (matches.Count == 0 && !IsLocaleMatch(targetLocale, "en"))
        {
            matches = candidates.Where(c => IsLocaleMatch(c.Data.LocaleCode, "en")).ToList();
            usingFallback = true;
        }

        return matches;
    }

    /// <summary>
    /// Injects the selected locale data into the locale resource and logs the results.
    /// </summary>
    private static int InjectAndLogLocales(
        Elements.Assets.LocaleResource localeData,
        List<(string Path, LocaleData Data)> toInject,
        bool usingFallback)
    {
        int messageCount = 0;

        foreach (var (file, data) in toInject)
        {
            localeData.LoadDataAdditively(data);
            messageCount += data.Messages.Count;

            string fileLocale = data.LocaleCode ?? "unknown";
            string fallbackSuffix = usingFallback ? " (fallback)" : string.Empty;
            Plugin.Log.LogDebug($"  - {Path.GetFileName(file)}: {fileLocale}, {data.Messages.Count} messages{fallbackSuffix}");
        }

        return messageCount;
    }

    /// <summary>
    /// Checks if the file's locale matches the target locale.
    /// Handles cases like "en-US" matching "en", or exact matches.
    /// </summary>
    private static bool IsLocaleMatch(string fileLocale, string targetLocale)
    {
        if (string.IsNullOrEmpty(fileLocale) || string.IsNullOrEmpty(targetLocale))
            return false;

        fileLocale = fileLocale.ToLowerInvariant();
        targetLocale = targetLocale.ToLowerInvariant();

        if (fileLocale == targetLocale)
            return true;

        string fileBase = Elements.Assets.LocaleResource.GetMainLanguage(fileLocale);
        string targetBase = Elements.Assets.LocaleResource.GetMainLanguage(targetLocale);

        return fileBase == targetBase;
    }
}