using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using BepInEx;
using BepInEx.NET.Common;
using Elements.Assets;
using FrooxEngine;
using HarmonyLib;

namespace BepisLocaleLoader;

[HarmonyPatch(typeof(FrooxEngine.LocaleResource), "LoadTargetVariant")]
internal static class LocaleInjectionPatch
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

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
    private static async Task Postfix(Task __result, FrooxEngine.LocaleResource __instance, LocaleVariantDescriptor variant, bool __state)
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
            InjectAllPluginLocales(__instance.Data);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to inject mod locales: {ex}");
        }

        if (__state)
        {
            try
            {
                OnLoadStateChangedMethod.Invoke(__instance, null);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to notify locale observers: {ex}");
            }
        }
    }

    private static void InjectAllPluginLocales(Elements.Assets.LocaleResource localeData)
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
            string? pluginDir = Path.GetDirectoryName(plugin.Location);
            if (string.IsNullOrEmpty(pluginDir))
                continue;

            string localeDir = Path.Combine(pluginDir, "Locale");
            if (!Directory.Exists(localeDir))
                continue;

            Plugin.Log.LogDebug($"Loading locales from {plugin.Metadata?.GUID ?? "unknown"}");

            foreach (string file in Directory.GetFiles(localeDir, "*.json", SearchOption.AllDirectories))
            {
                LocaleData? data = LoadLocaleDataFromFile(file);
                if (data == null)
                    continue;

                localeData.LoadDataAdditively(data);
                messageCount += data.Messages.Count;
                Plugin.Log.LogDebug($"  - {Path.GetFileName(file)}: {data.LocaleCode}, {data.Messages.Count} messages");
            }

            LocaleLoader.PluginsWithLocales.Add(plugin);
            pluginCount++;
        }

        if (pluginCount > 0)
            Plugin.Log.LogInfo($"Injected {messageCount} locale messages from {pluginCount} plugins");
    }

    private static LocaleData? LoadLocaleDataFromFile(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Error reading locale file {path}: {ex}");
            return null;
        }

        LocaleData? localeData;
        try
        {
            localeData = JsonSerializer.Deserialize<LocaleData>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Error parsing locale file {path}: {ex}");
            return null;
        }

        if (localeData?.Messages == null)
        {
            Plugin.Log.LogError($"Invalid locale file (missing messages): {path}");
            return null;
        }

        return localeData;
    }
}
