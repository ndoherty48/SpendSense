using System.Globalization;

using SpendSense.Components.Shared;

namespace SpendSense.Tests.Unit;

public class MoneyFormatTests
{
    // MoneyFormat uses the current culture for separators; pin it so the tests don't depend on the machine.
    static string Format(double amount, string symbol = "£", bool hidden = false, bool signed = false, int decimals = 2)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        try
        {
            return MoneyFormat.Format(amount, symbol, hidden, signed, decimals);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(1234.5, "£1,234.50")]
    [InlineData(0, "£0.00")]
    [InlineData(0.004, "£0.00")]
    public void Positive_amounts_have_separators_and_two_decimals(double amount, string expected)
    {
        Assert.Equal(expected, Format(amount));
    }

    [Fact]
    public void Negative_amounts_use_a_real_minus_sign_not_a_hyphen()
    {
        Assert.Equal("−£62.10", Format(-62.1));
    }

    [Fact]
    public void Signed_adds_a_plus_to_positive_amounts_only()
    {
        Assert.Equal("+£180.00", Format(180, signed: true));
        Assert.Equal("£0.00", Format(0, signed: true));
        Assert.Equal("−£5.00", Format(-5, signed: true));
    }

    [Fact]
    public void Hidden_masks_the_amount_but_keeps_the_symbol()
    {
        Assert.Equal("£•••", Format(-1234.5, hidden: true, signed: true));
    }

    [Fact]
    public void Decimals_and_symbol_are_configurable()
    {
        Assert.Equal("€2,000", Format(2000, symbol: "€", decimals: 0));
    }
}
