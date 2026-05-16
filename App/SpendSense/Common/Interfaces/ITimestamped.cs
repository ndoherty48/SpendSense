namespace SpendSense.Common.Interfaces;

public interface ITimestamped
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}