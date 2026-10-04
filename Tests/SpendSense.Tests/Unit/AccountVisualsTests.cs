using SpendSense.Common.Models.Enums;
using SpendSense.Components.Shared;

namespace SpendSense.Tests.Unit;

public class AccountVisualsTests
{
    [Theory]
    [InlineData("#33D6A6", "#33D6A6")]
    [InlineData("#fff", "#fff")]
    [InlineData("#33D6A680", "#33D6A680")]
    [InlineData("red", null)]
    [InlineData("#33D6A6; background:url(x)", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Only_hex_colours_reach_the_style_attribute(string? input, string? expected)
    {
        Assert.Equal(expected, AccountVisuals.SafeColor(input));
    }

    [Fact]
    public void Every_account_type_has_an_icon_label_and_default_colour()
    {
        foreach (var type in Enum.GetValues<AccountTypeEnum>())
        {
            Assert.False(string.IsNullOrEmpty(AccountVisuals.Icon(type)));
            Assert.False(string.IsNullOrEmpty(AccountVisuals.Label(type)));
            Assert.NotNull(AccountVisuals.SafeColor(AccountVisuals.DefaultColor(type)));
        }
    }
}
