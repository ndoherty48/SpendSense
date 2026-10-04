using SpendSense.Common.Services;

namespace SpendSense.Tests.TestDoubles;

/// <summary>Records notifications instead of showing them.</summary>
public sealed class RecordingNotifier : INotifier
{
    public List<(string Title, string Description)> Shown { get; } = [];

    public Task Show(string title, string description)
    {
        Shown.Add((title, description));
        return Task.CompletedTask;
    }
}
