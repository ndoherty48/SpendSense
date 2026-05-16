using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using SpendSense.Common.Interfaces;

namespace SpendSense.Common.Models;

public class Alert : ITimestamped
{
    [Key]
    public int Id {get;set;}
    [Required, MaxLength(200)]
    public required string Name {get;set;}
    [Required]
    public required string AlertType {get;set;}

    public int? CategoryId {get;set;}
    public int? GoalId {get;set;}
    public int? RecurringTransactionId {get;set;}
    public decimal? ThresholdPercentage {get;set;}
    public bool IsActive {get;set;}
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public Category? Category {get;set;}
    [ForeignKey(nameof(GoalId))]
    public Goal? Goal {get;set;}
    [ForeignKey(nameof(RecurringTransactionId))]
    public RecurringTransaction? RecurringTransaction {get;set;}

    // Navigation Properties
    public ICollection<AlertNotification> Notifications {get;set;} = [];
}
