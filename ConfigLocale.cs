using BepInEx.Configuration;
using Elements.Core;

namespace BepisLocaleLoader;

public struct ConfigLocale
{
    public ConfigLocale(string name, string description)
    {
        Name = name.AsLocaleKey();
        Description = description.AsLocaleKey();
    }

    public ConfigLocale(LocaleString name, LocaleString description)
    {
        Name = name;
        Description = description;
    }

    public LocaleString Name;
    public LocaleString Description;
}

public static class ConfigLocaleHelper
{
    /// <summary>
    /// Create a new setting with localized name and description. The resulting locale keys will be: Name = Settings.{guid}.{section}.{key}, Description = Settings.{guid}.{section}.{key}.Description
    /// </summary>
    /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
    /// <param name="config">The file that the setting will be bound to.</param>
    /// <param name="guid">The GUID of your mod, this is used to make the locale key unique from other plugins.</param>
    /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
    /// <param name="key">Name of the setting.</param>
    /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
    /// <param name="englishDescription">The english description of your config key. This will be visible when editing config files via mod managers or manually.</param>
    public static ConfigEntry<T> BindLocalized<T>(this ConfigFile config, string guid, string section, string key, T defaultValue, string englishDescription)
    {
        return config.BindLocalized(guid, new ConfigDefinition(section, key), defaultValue, new ConfigDescription(englishDescription));
    }

    /// <summary>
    /// Create a new setting with localized name and description. The resulting locale keys will be: Name = Settings.{guid}.{section}.{key}, Description = Settings.{guid}.{section}.{key}.Description
    /// </summary>
    /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
    /// <param name="config">The file that the setting will be bound to.</param>
    /// <param name="guid">The GUID of your mod, this is used to make the locale key unique from other plugins.</param>
    /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
    /// <param name="key">Name of the setting.</param>
    /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
    /// <param name="configDescription">Description and other metadata of the setting. The text description will be visible when editing config files via mod managers or manually.</param>
    public static ConfigEntry<T> BindLocalized<T>(this ConfigFile config, string guid, string section, string key, T defaultValue, ConfigDescription? configDescription = null)
    {
        return config.BindLocalized(guid, new ConfigDefinition(section, key), defaultValue, configDescription);
    }

    /// <summary>
    /// Create a new setting with localized name and description. The resulting locale keys will be: Name = Settings.{guid}.{Section}.{Key}, Description = Settings.{guid}.{Section}.{Key}.Description
    /// </summary>
    /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
    /// <param name="config">The file that the setting will be bound to.</param>
    /// <param name="guid">The GUID of your mod, this is used to make the locale key unique from other plugins.</param>
    /// <param name="configDefinition">Section and Key of the setting.</param>
    /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
    /// <param name="configDescription">Description and other metadata of the setting. The text description will be visible when editing config files via mod managers or manually.</param>
    public static ConfigEntry<T> BindLocalized<T>(this ConfigFile config, string guid, ConfigDefinition configDefinition, T defaultValue, ConfigDescription? configDescription = null)
    {
        string localeName = $"Settings.{guid}.{configDefinition.Section}.{configDefinition.Key}";
        string localeDescription = $"{localeName}.Description";
        var locale = new ConfigLocale(localeName, localeDescription);

        string description = configDescription?.Description ?? string.Empty;
        var acceptableValues = configDescription?.AcceptableValues;
        object[] tags = configDescription != null ? [.. configDescription.Tags, locale] : [locale];

        return config.Bind(configDefinition, defaultValue, new ConfigDescription(description, acceptableValues, tags));
    }
}