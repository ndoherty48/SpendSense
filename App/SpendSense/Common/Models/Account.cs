using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using SpendSense.Common.Interfaces;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Models;

/// <summary>
/// Where money is held: a current account, savings, a credit card or cash. Balances are computed from
/// <see cref="OpeningBalance"/> and transactions (see <c>AccountBalanceService</c>), never stored.
/// </summary>
public class Account : ITimestamped
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    [Required]
    public required AccountTypeEnum Type { get; set; }
    public int CurrencyId { get; set; }
    /// <summary>The balance before any recorded transaction. Negative for a card that was already owed money.</summary>
    public double OpeningBalance { get; set; }
    /// <summary>Credit cards only; null when the limit isn't tracked.</summary>
    public double? CreditLimit { get; set; }
    [MaxLength(9)]
    public string? Color { get; set; }
    /// <summary>Counted in the dashboard's Available total.</summary>
    public bool IncludeInAvailable { get; set; } = true;
    /// <summary>Pre-selected on new transactions. Exactly one account is the default.</summary>
    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }


    [ForeignKey(nameof(CurrencyId))]
    public Currency? Currency { get; set; }

    // Navigation Properties
    /// <summary>Transactions paid from (or, for income, into) this account.</summary>
    public ICollection<Transaction> Transactions { get; set; } = [];
    /// <summary>Transfers and savings whose destination is this account.</summary>
    public ICollection<Transaction> IncomingTransfers { get; set; } = [];
}
