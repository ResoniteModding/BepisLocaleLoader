using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using BepisResoniteWrapper;
using FrooxEngine;
using HarmonyLib;

namespace BepisLocaleLoader;

[ResonitePlugin(PluginMetadata.GUID, PluginMetadata.NAME, PluginMetadata.VERSION, PluginMetadata.AUTHORS, PluginMetadata.REPOSITORY_URL)]
[BepInDependency(BepInExResoniteShim.PluginMetadata.GUID, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    internal new static ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;

        ResoniteHooks.OnEngineReady += async () =>
        {
            await Task.Delay(5000);

            if (NetChainloader.Instance.Plugins.Count <= 0) return;

            NetChainloader.Instance.Plugins.Values.Do(LocaleLoader.AddLocaleFromPlugin);
        };

        Log.LogInfo($"Plugin {PluginMetadata.GUID} is loaded!");
    }
}