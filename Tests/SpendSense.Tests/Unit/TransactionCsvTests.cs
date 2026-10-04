using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Unit;

public class TransactionCsvTests
{
    static string Csv(params Transaction[] transactions)
    {
        using var writer = new StringWriter();
        TransactionCsv.Write(transactions, writer);
        return writer.ToString();
    }

    [Fact]
    public void Writes_a_header_and_one_row_per_transaction()
    {
        var t = Transaction(TransactionTypeEnum.Expense, 62.1, accountId: 1, description: "Whole Foods", date: new DateTime(2026, 10, 4));
        t.Account = Account(1, "Everyday");
        t.Category = new Category { Name = "Groceries", Type = TransactionTypeEnum.Expense };
        t.Currency = Gbp;

        var lines = Csv(t).Split("\r\n");

        Assert.Equal(TransactionCsv.Header, lines[0]);
        Assert.Equal("2026-10-04,Whole Foods,Expense,Groceries,Everyday,,-62.10,GBP,", lines[1]);
        Assert.Equal("", lines[2]);
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Income, 100)]
    [InlineData(TransactionTypeEnum.Expense, -100)]
    [InlineData(TransactionTypeEnum.Savings, -100)]
    [InlineData(TransactionTypeEnum.Transfer, -100)]
    public void Amounts_are_signed_from_the_source_account(TransactionTypeEnum type, double expected)
    {
        Assert.Equal(expected, TransactionCsv.SignedAmount(Transaction(type, 100, accountId: 1)));
    }

    [Fact]
    public void A_transfer_names_both_accounts()
    {
        var t = Transaction(TransactionTypeEnum.Transfer, 300, 1, 2, description: "Card payment", date: new DateTime(2026, 10, 3));
        t.Account = Account(1, "Everyday");
        t.ToAccount = Account(2, "Visa card", AccountTypeEnum.CreditCard);

        Assert.Equal("2026-10-03,Card payment,Transfer,,Everyday,Visa card,-300.00,,", Csv(t).Split("\r\n")[1]);
    }

    [Theory]
    [InlineData("Plain", "Plain")]
    [InlineData("Tesco, Leeds", "\"Tesco, Leeds\"")]
    [InlineData("The \"big\" shop", "\"The \"\"big\"\" shop\"")]
    [InlineData("Line\nbreak", "\"Line\nbreak\"")]
    [InlineData(" padded ", "\" padded \"")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Text_is_quoted_only_when_needed(string? value, string expected)
    {
        Assert.Equal(expected, TransactionCsv.Text(value));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+44 refund", "'+44 refund")]
    [InlineData("-5 fee", "'-5 fee")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public void Text_that_would_run_as_a_formula_is_neutralised(string value, string expected)
    {
        Assert.Equal(expected, TransactionCsv.Text(value));
    }

    [Fact]
    public void Amounts_use_invariant_formatting_whatever_the_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var row = Csv(Transaction(TransactionTypeEnum.Income, 1234.5, accountId: 1, date: new DateTime(2026, 1, 2))).Split("\r\n")[1];
            Assert.Contains(",1234.50,", row);
            Assert.StartsWith("2026-01-02,", row);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
