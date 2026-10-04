using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using SpendSense.Common.Interfaces;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Models;

public class Transaction : ITimestamped, IAccountMovement
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(500)]
    public required string Description { get; set; }
    public int? CategoryId {get;set;}
    [Required]
    public required double Amount { get; set; }
    public int CurrencyId{get;set;}
    /// <summary>The account the money comes out of (or, for income, goes into).</summary>
    public int AccountId {get;set;}
    /// <summary>The destination account: required for a transfer, optional for savings, otherwise null.</summary>
    public int? ToAccountId {get;set;}
    [Required]
    public required DateTime TransactionDate {get;set;}
    [Required]
    public required TransactionTypeEnum TransactionType {get;set;}
    public bool IsRecurringTransaction => RecurringTransactionId is not null;
    public int? RecurringTransactionId{get;set;}
    public string? Notes {get;set;}
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }


    [ForeignKey(nameof(RecurringTransactionId))]
    public RecurringTransaction? RecurringTransaction {get;set;}
    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; }
    [ForeignKey(nameof(CurrencyId))]
    public Currency? Currency { get; set; }
    [ForeignKey(nameof(AccountId))]
    public Account? Account { get; set; }
    [ForeignKey(nameof(ToAccountId))]
    public Account? ToAccount { get; set; }

    // Navigation Properties
    public ICollection<Tag>? Tags {get;set;}
    public ICollection<TransactionTag>? TransactionTags {get;set;}
}
