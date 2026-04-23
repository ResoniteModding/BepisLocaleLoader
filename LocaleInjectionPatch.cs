using System.Reflection.Emit;
using System.Reflection;
using BepInEx;
using BepInEx.NET.Common;
using Elements.Assets;
using FrooxEngine;
using HarmonyLib;

namespace BepisLocaleLoader;

/// <summary>
/// Harmony patch that injects mod locales into the freshly loaded locale resource before observers are notified.
/// </summary>
[HarmonyPatch(typeof(FrooxEngine.LocaleResource), "LoadTargetVariant")]
internal static class LocaleInjectionPatch
{
    private static readonly MethodInfo OnLoadStateChangedMethod =
        AccessTools.Method(typeof(FrooxEngine.Asset), "OnLoadStateChanged")
        ?? throw new MissingMethodException(typeof(FrooxEngine.Asset).FullName, "OnLoadStateChanged");

    [HarmonyPrefix]
    private static void Prefix(FrooxEngine.LocaleResource __instance, out bool __state)
        => __state = __instance.Data != null;

    [HarmonyTranspiler]
    [HarmonyPatch(MethodType.Async)]
    private static IEnumerable<CodeInstruction> LoadTargetVariantMoveNextTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(OnLoadStateChangedMethod))
            {
                yield return instruction;
                continue;
            }

            var pop = new CodeInstruction(OpCodes.Pop);
            pop.labels.AddRange(instruction.labels);
            pop.blocks.AddRange(instruction.blocks);
            yield return pop;
        }
    }

    [HarmonyPostfix]
    private static async Task Postfix(Task __result, FrooxEngine.LocaleResource __instance, LocaleVariantDescriptor? variant, bool __state)
    {
        try
        {
            await __result;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"LoadTargetVariant failed before locale injection: {ex}");
            return;
        }

        if (__instance.Data == null)
        {
            Plugin.Log.LogWarning("LoadTargetVariant completed but Data is null - skipping locale injection");
            return;
        }

        try
        {
            string? targetLocale = GetInjectionTargetLocale(variant);
            if (targetLocale != null)
            {
                InjectAllPluginLocales(__instance.Data, targetLocale);
            }

            LocaleLoader.ReplayRuntimeLocaleData(__instance.Data);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to inject mod locales: {ex}");
        }

        if (__state)
            OnLoadStateChangedMethod.Invoke(__instance, null);
    }

    private static string? GetInjectionTargetLocale(LocaleVariantDescriptor? variant)
    {
        string targetLocale = variant?.LocaleCode ?? "en";
        return targetLocale == "-" ? null : targetLocale;
    }

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

            var candidates = LoadLocaleFiles(localeFiles);
            var toInject = LocaleSelection.SelectMatchingLocales(
                candidates,
                candidate => LocaleSelection.GetLocaleFileStep(candidate.Path),
                targetLocale);
            if (toInject.Count == 0)
                continue;

            Plugin.Log.LogDebug($"Loading locales from {plugin.Metadata?.GUID ?? "unknown"}");

            messageCount += InjectAndLogLocales(localeData, toInject, targetLocale);

            LocaleLoader.TrackPluginWithLocale(plugin);
            pluginCount++;
        }

        if (pluginCount > 0)
        {
            Plugin.Log.LogInfo($"Injected {messageCount} locale messages from {pluginCount} plugins");
        }
    }

    private static List<(string Path, LocaleData Data)> LoadLocaleFiles(IEnumerable<string> localeFiles)
    {
        var candidates = new List<(string Path, LocaleData Data)>();

        foreach (string file in localeFiles)
        {
            var data = LocaleLoader.LoadLocaleDataFromFile(file);
            if (data != null)
                candidates.Add((file, data));
        }

        return candidates;
    }

    private static int InjectAndLogLocales(
        Elements.Assets.LocaleResource localeData,
        List<(string Path, LocaleData Data)> toInject,
        string targetLocale)
    {
        int messageCount = 0;

        foreach (var (file, data) in toInject)
        {
            LocaleLoader.TryApplyLocaleData(localeData, data, force: true);
            messageCount += data.Messages.Count;

            string fileLocaleStep = LocaleSelection.GetLocaleFileStep(file);
            string fileLocale = data.LocaleCode ?? "unknown";
            string fallbackSuffix = LocaleSelection.IsEnglishFallbackLocale(fileLocaleStep, targetLocale) ? " (fallback)" : string.Empty;
            Plugin.Log.LogDebug($"  - {Path.GetFileName(file)}: {fileLocale}, {data.Messages.Count} messages{fallbackSuffix}");
        }

        return messageCount;
    }
}
