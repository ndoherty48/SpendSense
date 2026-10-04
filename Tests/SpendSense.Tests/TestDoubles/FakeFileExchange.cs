using SpendSense.Common.Services;

namespace SpendSense.Tests.TestDoubles;

/// <summary>Records what was shared, and hands back <see cref="NextPick"/> when a file is picked.</summary>
public sealed class FakeFileExchange : IFileExchange
{
    public List<(string Path, string Title)> Shared { get; } = [];

    /// <summary>The path the next pick returns; null means the user cancelled.</summary>
    public string? NextPick { get; set; }

    public Task Share(string path, string title)
    {
        Shared.Add((path, title));
        return Task.CompletedTask;
    }

    public Task<string?> Pick(string title) => Task.FromResult(NextPick);
}
