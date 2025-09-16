using Elements.Core;

namespace BepisLocaleLoader;

// We could maybe add more things to this later if needed
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