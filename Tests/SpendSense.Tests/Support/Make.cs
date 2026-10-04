using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;

namespace SpendSense.Tests.Support;

/// <summary>Terse builders for entities, so each test shows only the fields it cares about.</summary>
public static class Make
{
    public static readonly Currency Gbp = new() { Id = 1, Code = "GBP", Name = "British Pound", Symbol = "£", IsDefault = true };
    public static readonly Currency Eur = new() { Id = 2, Code = "EUR", Name = "Euro", Symbol = "€" };

    public static Account Account(int id, string name, AccountTypeEnum type = AccountTypeEnum.Current,
        double opening = 0, double? limit = null, Currency? currency = null, bool includeInAvailable = true,
        bool isDefault = false, bool archived = false, int? parentId = null)
    {
        currency ??= Gbp;
        return new Account
        {
            Id = id, Name = name, Type = type, OpeningBalance = opening, CreditLimit = limit,
            CurrencyId = currency.Id, Currency = currency, IncludeInAvailable = includeInAvailable,
            IsDefault = isDefault, IsArchived = archived, ParentAccountId = parentId
        };
    }

    public static AccountBalance Balance(Account account, double balance) => new(account, balance);

    public static Transaction Transaction(TransactionTypeEnum type, double amount, int accountId, int? toAccountId = null,
        string description = "Test", DateTime? date = null, int currencyId = 1, int? categoryId = null) => new()
    {
        Description = description,
        Amount = amount,
        TransactionType = type,
        TransactionDate = date ?? DateTime.Today,
        CurrencyId = currencyId,
        AccountId = accountId,
        ToAccountId = toAccountId,
        CategoryId = categoryId
    };
}
