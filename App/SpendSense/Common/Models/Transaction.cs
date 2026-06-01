using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using SpendSense.Common.Interfaces;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Models;

public class Transaction : ITimestamped
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(500)]
    public required string Description { get; set; }
    public int? CategoryId {get;set;}
    [Required]
    public required double Amount { get; set; }
    public int CurrencyId{get;set;}
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

    // Navigation Properties
    public ICollection<Tag>? Tags {get;set;}
    public ICollection<TransactionTag>? TransactionTags {get;set;}
}
