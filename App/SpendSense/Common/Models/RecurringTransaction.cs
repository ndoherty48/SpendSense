using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using SpendSense.Common.Interfaces;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Models;

public class RecurringTransaction : ITimestamped
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public required string Name { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public int? CategoryId {get;set;}
    public required double Amount { get; set; }
    public int CurrencyId{get;set;}
    [Required]
    public required FrequencyEnum Frequency { get; set; }
    [Required]
    public required TransactionTypeEnum TransactionType { get; set; }
    [Required]
    public required DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int? DayOfMonth { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }


    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; }
    [ForeignKey(nameof(CurrencyId))]
    public Currency? Currency { get; set; }

    // Navigation Properties
    public ICollection<Transaction> Transactions {get;set;} = [];
    public ICollection<Alert> Alerts {get;set;} = [];
}
