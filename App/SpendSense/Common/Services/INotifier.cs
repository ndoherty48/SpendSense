namespace SpendSense.Common.Services;

/// <summary>Shows a local notification. Abstracted so the alert rules can be tested without a device.</summary>
public interface INotifier
{
    Task Show(string title, string description);
}
