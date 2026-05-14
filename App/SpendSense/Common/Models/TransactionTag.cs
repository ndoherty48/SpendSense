using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendSense.Common.Models;

public class TransactionTag
{
    [Key]
    public int Id {get;set;}
    [Required]
    public required int TransactionId {get;set;}
    [Required]
    public required int TagId {get;set;}
    

    [ForeignKey(nameof(TransactionId))]
    public required Transaction Transaction {get;set;}
    [ForeignKey(nameof(TagId))]
    public required Tag Tag {get;set;}
}
