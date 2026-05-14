using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendSense.Common.Models;

public class AlertNotification
{
    [Key]
    public int Id {get;set;}
    public required int AlertId {get;set;}
    [Required, MaxLength(1_000)]
    public required string Message {get;set;}
    public required DateTime TriggeredAt {get;set;}
    public bool IsRead {get;set;}

    [ForeignKey(nameof(AlertId))]
    public required Alert Alert {get;set;}
}