using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using BepisResoniteWrapper;
using HarmonyLib;

namespace BepisLocaleLoader;

[BepInAutoPlugin]
[BepInDependency(BepInExResoniteShim.PluginMetadata.GUID, BepInDependency.DependencyFlags.HardDependency)]
public partial class Plugin : BasePlugin
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

        Log.LogInfo($"Plugin {GUID} is loaded!");
    }
}