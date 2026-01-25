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

            Plugin.Log.LogDebug($"Injecting mod locales after LoadTargetVariant completed (target: {targetLocale})");

            InjectAllPluginLocales(__instance.Data, targetLocale);
        }
        catch (Exception ex)
        {
            // async void cannot propagate exceptions and unhandled ones may crash the app
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

            foreach (string file in localeFiles)
            {
                int injected = InjectLocaleFile(localeData, file, targetLocale);
                if (injected > 0)
                {
                    messageCount += injected;
                }
            }

            LocaleLoader.TrackPluginWithLocale(plugin);
            pluginCount++;
        }

        if (pluginCount > 0)
        {
            Plugin.Log.LogInfo($"Injected {messageCount} locale messages from {pluginCount} plugins");
        }
    }

    /// <summary>
    /// Loads and injects a single locale file into the target locale resource.
    /// </summary>
    /// <returns>Number of messages injected, or 0 on failure</returns>
    private static int InjectLocaleFile(Elements.Assets.LocaleResource localeData, string filePath, string targetLocale)
    {
        var data = LocaleLoader.LoadLocaleDataFromFile(filePath);
        if (data == null) return 0;

        localeData.LoadDataAdditively(data);

        string fileLocale = data.LocaleCode ?? "unknown";
        bool isMatch = IsLocaleMatch(fileLocale, targetLocale);

        Plugin.Log.LogDebug($"  - {Path.GetFileName(filePath)}: {fileLocale}, {data.Messages.Count} messages{(isMatch ? "" : " (fallback)")}");

        return data.Messages.Count;
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

        // Base language match (e.g., "en-us" matches "en")
        string fileBase = Elements.Assets.LocaleResource.GetMainLanguage(fileLocale);
        string targetBase = Elements.Assets.LocaleResource.GetMainLanguage(targetLocale);

        return fileBase == targetBase;
    }
}
