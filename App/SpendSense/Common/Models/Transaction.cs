using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendSense.Common.Models;

public class Transaction
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
    public bool IsRecurringTransaction => RecurringTransaction is not null;
    public int RecurringTransactionId{get;set;}
    public string? Notes {get;set;}
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;    


    [ForeignKey(nameof(RecurringTransactionId))]
    public RecurringTransaction? RecurringTransaction {get;set;}
    [ForeignKey(nameof(CategoryId))]
    public required Category Category { get; set; }
    [ForeignKey(nameof(CurrencyId))]
    public required Currency Currency { get; set; }

    // Navigation Properties
    public ICollection<Tag>? Tags {get;set;}
    public ICollection<TransactionTag>? TransactionTags {get;set;}
}
