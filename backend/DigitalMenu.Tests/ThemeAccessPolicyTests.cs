using DigitalMenu.Domain;

namespace DigitalMenu.Tests;

public sealed class ThemeAccessPolicyTests
{
    [Fact]
    public void Start_plan_only_exposes_classic_themes()
    {
        var available = ThemeAccessPolicy.AvailableThemeKeys("Start", "premium-gold,coffee-cream");

        Assert.Equal([ThemeAccessPolicy.ClassicLight, ThemeAccessPolicy.ClassicDark], available);
    }

    [Fact]
    public void Pro_plan_exposes_classic_and_two_selected_themes()
    {
        var additional = ThemeAccessPolicy.ValidateAdditionalThemes("Pro", ["premium-gold", "coffee-cream"]);
        var available = ThemeAccessPolicy.AvailableThemeKeys("Pro", ThemeAccessPolicy.SerializeAdditionalThemes(additional));

        Assert.Equal(
            [ThemeAccessPolicy.ClassicLight, ThemeAccessPolicy.ClassicDark, "premium-gold", "coffee-cream"],
            available);
    }

    [Theory]
    [InlineData()]
    [InlineData("premium-gold")]
    public void Pro_plan_requires_exactly_two_additional_themes(params string[] themes)
    {
        Assert.Throws<InvalidOperationException>(() => ThemeAccessPolicy.ValidateAdditionalThemes("Pro", themes));
    }

    [Fact]
    public void Premium_plan_exposes_the_complete_catalog()
    {
        var available = ThemeAccessPolicy.AvailableThemeKeys("Premium", null);

        Assert.Equal(ThemeAccessPolicy.SupportedThemeKeys, available);
        Assert.Contains("classic-dark", available);
        Assert.Contains("modern-dark", available);
    }
}
