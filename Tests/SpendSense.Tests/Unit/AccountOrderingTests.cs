using SpendSense.Common.Data.Repositories;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Unit;

public class AccountOrderingTests
{
    [Fact]
    public void Default_first_then_current_cash_cards_savings()
    {
        var accounts = new[]
        {
            Account(1, "Savings", AccountTypeEnum.Savings),
            Account(2, "Card", AccountTypeEnum.CreditCard),
            Account(3, "Cash", AccountTypeEnum.Cash),
            Account(4, "Current"),
            Account(5, "Main savings", AccountTypeEnum.Savings, isDefault: true)
        };

        var names = accounts.InDisplayOrder().Select(a => a.Name);

        Assert.Equal(["Main savings", "Current", "Cash", "Card", "Savings"], names);
    }

    [Fact]
    public void Same_type_sorts_by_sort_order_then_name_ignoring_case()
    {
        var b = Account(1, "bravo");
        var a = Account(2, "Alpha");
        var first = Account(3, "Zulu");
        first.SortOrder = -1;

        Assert.Equal(["Zulu", "Alpha", "bravo"], new[] { b, a, first }.InDisplayOrder().Select(x => x.Name));
    }

    [Fact]
    public void Pots_follow_straight_after_their_parent()
    {
        var accounts = new[]
        {
            Account(1, "Everyday", isDefault: true),
            Account(2, "Monzo"),
            Account(3, "Holiday", AccountTypeEnum.Savings, parentId: 2),
            Account(4, "Bills", parentId: 2),
            Account(5, "Visa", AccountTypeEnum.CreditCard)
        };

        Assert.Equal(["Everyday", "Monzo", "Bills", "Holiday", "Visa"], accounts.InDisplayOrder().Select(a => a.Name));
    }

    [Fact]
    public void A_pot_whose_parent_is_missing_stands_on_its_own()
    {
        // e.g. the parent is archived and only open accounts were loaded
        var orphan = Account(3, "Holiday", AccountTypeEnum.Savings, parentId: 99);

        Assert.Equal(["Holiday"], new[] { orphan }.InDisplayOrder().Select(a => a.Name));
    }

    [Fact]
    public void Group_pairs_each_top_level_account_with_its_pots()
    {
        var monzo = Account(2, "Monzo");
        IReadOnlyList<AccountBalance> balances =
        [
            Balance(Account(1, "Everyday"), 100),
            Balance(monzo, 440),
            Balance(Account(3, "Bills", parentId: 2), 300),
            Balance(Account(4, "Holiday", AccountTypeEnum.Savings, parentId: 2), 700)
        ];

        var groups = AccountBalanceService.Group(balances);

        Assert.Equal(2, groups.Count);
        Assert.False(groups[0].HasPots);
        Assert.Equal(100, groups[0].Total);
        Assert.Same(monzo, groups[1].Account.Account);
        Assert.Equal(["Bills", "Holiday"], groups[1].Pots.Select(p => p.Account.Name));
        Assert.Equal(1440, groups[1].Total);
    }
}
