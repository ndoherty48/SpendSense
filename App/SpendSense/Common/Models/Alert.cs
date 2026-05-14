using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendSense.Common.Models;

public class Alert
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;  

    [ForeignKey(nameof(CategoryId))]
    public Category? Category {get;set;}
    [ForeignKey(nameof(GoalId))]
    public Goal? Goal {get;set;}
    [ForeignKey(nameof(RecurringTransactionId))]
    public RecurringTransaction? RecurringTransaction {get;set;}

    // Navigation Properties
    public ICollection<AlertNotification> Notifications {get;set;} = [];
}
