using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class Tag
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    [MaxLength(7)]
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<Transaction> Transactions {get;set;} = [];
    public ICollection<TransactionTag> TransactionTags {get;set;} = [];
}
