using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace SpendSense.Tests.TestDoubles;

/// <summary>Remembers the last text copied: MAUI's clipboard needs a device.</summary>
public sealed class FakeClipboard : IClipboard
{
    public string? Text { get; private set; }

    public bool HasText => Text is not null;

    public event EventHandler<EventArgs>? ClipboardContentChanged;

    public Task SetTextAsync(string? text)
    {
        Text = text;
        ClipboardContentChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync() => Task.FromResult(Text);
}
