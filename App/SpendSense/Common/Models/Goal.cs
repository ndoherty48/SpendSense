using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendSense.Common.Models;

public class Goal
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public required string Name { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public int? CategoryId {get;set;}
    public int CurrencyId{get;set;}
    public required double TargetAmount { get; set; }
    public double CurrentAmount { get; set; } = 0.0;
    public DateTime? TargetDate { get; set; }
    public PriorityEnum Priority { get; set; } = PriorityEnum.Low;
    public StatusEnum Status { get; set; } = StatusEnum.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; }
    [ForeignKey(nameof(CurrencyId))]
    public Currency? Currency { get; init; }

    // Navigation Properties
    public ICollection<Alert> Alerts {get;set;} = [];
}
