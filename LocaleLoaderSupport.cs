using FrooxEngine;

namespace BepisLocaleLoader;

internal static class LocaleMutationRules
{
    internal static bool ShouldSkipMessages(IEnumerable<string> existingKeys, IEnumerable<string> incomingKeys, bool force)
    {
        if (force)
            return false;

        string? firstKey = incomingKeys.FirstOrDefault();
        return firstKey != null && existingKeys.Contains(firstKey);
    }

    internal static bool TryApplyMessages(
        IDictionary<string, string> existingMessages,
        IEnumerable<KeyValuePair<string, string>> incomingMessages,
        bool force)
    {
        var materializedMessages = incomingMessages.ToList();
        if (ShouldSkipMessages(existingMessages.Keys, materializedMessages.Select(message => message.Key), force))
            return true;

        foreach (var message in materializedMessages)
        {
            if (!existingMessages.ContainsKey(message.Key))
                existingMessages.Add(message.Key, message.Value);
        }

        return true;
    }
}

internal static class LocaleSelection
{
    internal static List<T> SelectMatchingLocales<T>(
        IEnumerable<T> candidates,
        Func<T, string> getLocaleFileStep,
        string targetLocale)
    {
        var materialized = candidates.ToList();
        var matches = new List<T>();

        foreach (string localeCode in GetLocaleLoadChain(targetLocale))
        {
            matches.AddRange(materialized.Where(candidate => IsExactLocaleMatch(getLocaleFileStep(candidate), localeCode)));
        }

        return matches;
    }

    internal static string GetLocaleFileStep(string path)
        => Path.GetFileNameWithoutExtension(path) ?? string.Empty;

    internal static bool IsEnglishFallbackLocale(string? fileLocale, string targetLocale)
        => IsLanguageFamilyMatch(fileLocale, "en")
        && !IsLanguageFamilyMatch(targetLocale, "en");

    private static IEnumerable<string> GetLocaleLoadChain(string targetLocale)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string localeCode in GetLoadChainCandidates(targetLocale))
        {
            if (seen.Add(localeCode))
                yield return localeCode;
        }
    }

    private static IEnumerable<string> GetLoadChainCandidates(string targetLocale)
    {
        if (string.IsNullOrWhiteSpace(targetLocale))
        {
            yield return "en";
            yield break;
        }

        yield return targetLocale;

        string baseLanguage = GetMainLanguage(targetLocale);
        if (!string.Equals(baseLanguage, targetLocale, StringComparison.OrdinalIgnoreCase))
            yield return baseLanguage;

        if (!string.Equals(baseLanguage, "en", StringComparison.OrdinalIgnoreCase))
            yield return "en";
    }

    private static bool IsExactLocaleMatch(string? fileLocale, string? targetLocale)
        => !string.IsNullOrWhiteSpace(fileLocale)
        && !string.IsNullOrWhiteSpace(targetLocale)
        && string.Equals(fileLocale, targetLocale, StringComparison.OrdinalIgnoreCase);

    private static bool IsLanguageFamilyMatch(string? fileLocale, string targetLocale)
        => !string.IsNullOrWhiteSpace(fileLocale)
        && string.Equals(GetMainLanguage(fileLocale), GetMainLanguage(targetLocale), StringComparison.OrdinalIgnoreCase);

    private static string GetMainLanguage(string localeCode)
    {
        int index = localeCode.IndexOf('-');
        return index < 0 ? localeCode.ToLowerInvariant() : localeCode[..index].ToLowerInvariant();
    }
}

internal static class LocaleRuntimeFlushGate
{
    internal static bool CanFlushRuntimeQueue(FrooxEngine.LocaleResource localeAsset)
        => CanFlushRuntimeQueue(
            hasData: localeAsset.Data != null,
            forceUpdate: localeAsset.ForceUpdate,
            loadedLocaleCode: localeAsset.LoadedVariant?.LocaleCode,
            targetLocaleCode: localeAsset.TargetVariant?.LocaleCode);

    internal static bool CanFlushRuntimeQueue(
        bool hasData,
        bool forceUpdate,
        string? loadedLocaleCode,
        string? targetLocaleCode)
    {
        if (!hasData || forceUpdate)
            return false;

        return !string.IsNullOrWhiteSpace(loadedLocaleCode)
            && !string.IsNullOrWhiteSpace(targetLocaleCode)
            && string.Equals(loadedLocaleCode, targetLocaleCode, StringComparison.OrdinalIgnoreCase);
    }
}
