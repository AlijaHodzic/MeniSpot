namespace DigitalMenu.Domain;

public static class ThemeAccessPolicy
{
    public const string ClassicLight = "classic-light";
    public const string ClassicDark = "classic-dark";

    public static readonly IReadOnlyList<string> ClassicThemeKeys = [ClassicLight, ClassicDark];

    public static readonly IReadOnlyList<string> SupportedThemeKeys =
    [
        ClassicLight, ClassicDark,
        "premium-gold", "burgundy-dining", "mediterranean-blue", "olive-linen", "ocean-slate",
        "coffee-cream", "urban-espresso", "soft-pastel", "natural-green", "rose-latte", "cocoa-mint",
        "neon-night", "royal-violet",
        "warm-orange", "street-red", "yellow-pop", "burger-black", "lime-street", "modern-dark"
    ];

    public static bool IsSupported(string themeKey) => SupportedThemeKeys.Contains(themeKey);

    public static IReadOnlyList<string> ParseAdditionalThemes(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsAdditionalTheme)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

    public static string SerializeAdditionalThemes(IEnumerable<string>? themes) =>
        string.Join(',', NormalizeAdditionalThemes(themes, requireTwo: false));

    public static IReadOnlyList<string> ValidateAdditionalThemes(string plan, IEnumerable<string>? themes)
    {
        var normalized = NormalizeAdditionalThemes(themes, requireTwo: plan == "Pro");
        return plan == "Pro" ? normalized : [];
    }

    public static IReadOnlyList<string> AvailableThemeKeys(string plan, string? additionalThemeKeys) => plan switch
    {
        "Premium" => SupportedThemeKeys,
        "Pro" => ClassicThemeKeys.Concat(ParseAdditionalThemes(additionalThemeKeys)).Distinct().ToArray(),
        _ => ClassicThemeKeys
    };

    public static bool IsAllowed(string plan, string? additionalThemeKeys, string themeKey) =>
        AvailableThemeKeys(plan, additionalThemeKeys).Contains(themeKey);

    private static IReadOnlyList<string> NormalizeAdditionalThemes(IEnumerable<string>? themes, bool requireTwo)
    {
        var normalized = (themes ?? [])
            .Where(IsAdditionalTheme)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        if (normalized.Length > 2)
            throw new InvalidOperationException("Pro plan supports exactly two additional themes.");
        if (requireTwo && normalized.Length != 2)
            throw new InvalidOperationException("Pro plan requires exactly two additional themes.");
        return normalized;
    }

    private static bool IsAdditionalTheme(string themeKey) =>
        IsSupported(themeKey) && !ClassicThemeKeys.Contains(themeKey);
}
