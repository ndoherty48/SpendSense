using Bunit;

using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Components.Pages.Transactions;
using SpendSense.Tests.Support;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Components;

/// <summary>The form's own rules: transfer handling, currency from the account, and blocking bad submits.</summary>
public class TransactionFormTests : ComponentTest
{
    static readonly IReadOnlyList<AccountBalance> Accounts =
    [
        Balance(Account(1, "Everyday"), 1000),
        Balance(Account(2, "Visa card", AccountTypeEnum.CreditCard), -600),
        Balance(Account(4, "Euro cash", AccountTypeEnum.Cash, currency: Eur), 50)
    ];

    int submitted;

    IRenderedComponent<TransactionForm> RenderForm(Transaction model, bool preview = false) =>
        Render<TransactionForm>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Categories, Array.Empty<Category>())
            .Add(x => x.Accounts, Accounts)
            .Add(x => x.Title, "New transaction")
            .Add(x => x.SubmitLabel, "Add transaction")
            .Add(x => x.ShowPreview, preview)
            .Add(x => x.OnSubmit, () => submitted++));

    [Fact]
    public void Transfer_hides_the_category_and_explains_budgets_are_unaffected()
    {
        var cut = RenderForm(Transaction(TransactionTypeEnum.Expense, 10, accountId: 1));

        cut.FindAll("button.segment").Single(b => b.TextContent == "Transfer").Click();

        Assert.DoesNotContain("Category", cut.Find(".fields").TextContent);
        Assert.Contains("don't count as income, spending or savings", cut.Markup);
    }

    [Fact]
    public void Transfer_preview_shows_both_balances_after()
    {
        var cut = RenderForm(Transaction(TransactionTypeEnum.Transfer, 300, accountId: 1, toAccountId: 2), preview: true);

        Assert.Equal("After: Everyday £700.00 · Visa card owes £300.00", cut.Find(".preview").TextContent);
    }

    [Fact]
    public void A_transfer_to_the_same_account_is_not_submitted()
    {
        var model = Transaction(TransactionTypeEnum.Transfer, 10, accountId: 1, toAccountId: 1);
        var cut = RenderForm(model);

        cut.Find("form").Submit();

        Assert.Equal(0, submitted);
        Assert.Equal("Choose a different account to move money into.", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void A_transfer_without_a_description_gets_one_and_loses_its_category()
    {
        var model = Transaction(TransactionTypeEnum.Transfer, 300, accountId: 1, toAccountId: 2, description: "", categoryId: 7);
        var cut = RenderForm(model);

        cut.Find("form").Submit();

        Assert.Equal(1, submitted);
        Assert.Equal("Transfer to Visa card", model.Description);
        Assert.Null(model.CategoryId);
    }

    [Fact]
    public void Submitting_takes_the_currency_from_the_account_and_drops_a_stray_destination()
    {
        var model = Transaction(TransactionTypeEnum.Expense, 10, accountId: 4, toAccountId: 2, currencyId: 1);
        var cut = RenderForm(model);

        cut.Find("form").Submit();

        Assert.Equal(1, submitted);
        Assert.Equal(Eur.Id, model.CurrencyId);
        Assert.Null(model.ToAccountId);
    }
}
