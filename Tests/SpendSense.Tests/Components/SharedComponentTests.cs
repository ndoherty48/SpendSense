using Bunit;

using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Components.Shared;
using SpendSense.Tests.Support;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Components;

public class SharedComponentTests : ComponentTest
{
    [Fact]
    public void Money_shows_a_real_minus_sign()
    {
        var cut = Render<Money>(p => p.Add(x => x.Amount, -62.1));

        Assert.Equal("−£62.10", cut.Find(".money").TextContent);
    }

    [Fact]
    public void Money_signed_income_gets_a_plus_and_the_income_tone()
    {
        var cut = Render<Money>(p => p.Add(x => x.Amount, 180).Add(x => x.Signed, true).Add(x => x.Tone, MoneyTone.Income));

        var money = cut.Find(".money");
        Assert.Equal("+£180.00", money.TextContent);
        Assert.Contains("money--income", money.ClassList);
    }

    [Fact]
    public void Hidden_amounts_are_masked_and_read_as_hidden()
    {
        Settings.HideAmounts = true;

        var cut = Render<Money>(p => p.Add(x => x.Amount, 1234.5));

        Assert.Equal("£•••", cut.Find(".money").TextContent);
        Assert.Equal("true", cut.Find(".money").GetAttribute("aria-hidden"));
        Assert.Equal("Amount hidden", cut.Find(".ss-sr-only").TextContent);
        Assert.DoesNotContain("1,234", cut.Markup);
    }

    [Fact]
    public void Progress_bar_exposes_its_value_and_flags_overspend()
    {
        var cut = Render<ProgressBar>(p => p.Add(x => x.Percent, 173).Add(x => x.Label, "Dining").Add(x => x.ValueText, "£260 of £150"));

        var bar = cut.Find("[role=progressbar]");
        Assert.Equal("100", bar.GetAttribute("aria-valuenow"));
        Assert.Equal("£260 of £150", bar.GetAttribute("aria-valuetext"));
        Assert.Contains("progress--over", bar.ClassList);
    }

    [Fact]
    public void Segmented_control_with_links_marks_the_current_page()
    {
        IReadOnlyList<Segment<string>> segments = [new("/transactions", "All", "/transactions"), new("/recurring-transactions", "Recurring", "/recurring-transactions")];

        var cut = Render<SegmentedControl<string>>(p => p.Add(x => x.Segments, segments).Add(x => x.Value, "/recurring-transactions"));

        var current = cut.Find("a[aria-current=page]");
        Assert.Equal("Recurring", current.TextContent);
        Assert.Equal(2, cut.FindAll("a.segment").Count);
    }

    [Fact]
    public void Segmented_control_with_state_reports_the_choice()
    {
        IReadOnlyList<Segment<TransactionTypeEnum>> segments = [new(TransactionTypeEnum.Expense, "Expense"), new(TransactionTypeEnum.Transfer, "Transfer")];
        TransactionTypeEnum? chosen = null;

        var cut = Render<SegmentedControl<TransactionTypeEnum>>(p => p
            .Add(x => x.Segments, segments)
            .Add(x => x.Value, TransactionTypeEnum.Expense)
            .Add(x => x.ValueChanged, v => chosen = v));
        cut.FindAll("button.segment")[1].Click();

        Assert.Equal(TransactionTypeEnum.Transfer, chosen);
        Assert.Equal("true", cut.FindAll("button.segment")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Account_card_for_a_credit_card_shows_owed_and_credit_left()
    {
        var item = Balance(Account(6, "Visa card", AccountTypeEnum.CreditCard, limit: 2000), -612.30);

        var cut = Render<AccountCard>(p => p.Add(x => x.Item, item));

        Assert.Equal("/accounts/6", cut.Find("a.account-card").GetAttribute("href"));
        Assert.Contains("£612.30", cut.Markup);
        Assert.Contains("owed", cut.Markup);
        Assert.Contains("£1,387.70", cut.Markup);
        Assert.NotNull(cut.Find("[role=progressbar]"));
    }

    [Fact]
    public void Account_card_with_pots_shows_the_combined_total()
    {
        var monzo = Balance(Account(2, "Monzo"), 440);
        IReadOnlyList<AccountBalance> pots = [Balance(Account(3, "Bills", parentId: 2), 300), Balance(Account(4, "Holiday", parentId: 2), 700)];

        var cut = Render<AccountCard>(p => p.Add(x => x.Item, monzo).Add(x => x.Pots, pots));

        Assert.Contains("£1,440.00", cut.Markup);
        Assert.Contains("Incl. 2 pots", cut.Markup);
    }

    [Fact]
    public void Icon_tile_ignores_a_colour_that_is_not_hex()
    {
        var cut = Render<IconTile>(p => p.Add(x => x.Icon, "<path/>").Add(x => x.Color, "red;background:url(x)"));

        Assert.DoesNotContain("url(x)", cut.Markup);
        Assert.DoesNotContain("icon-tile--custom", cut.Markup);
    }
}
