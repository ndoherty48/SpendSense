using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace SpendSense.Common.Services;

/// <summary>The share sheet and file picker of the platform.</summary>
public class MauiFileExchange(IShare share, IFilePicker picker, IFileSystem fileSystem) : IFileExchange
{
    public Task Share(string path, string title) =>
        share.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path) });

    public async Task<string?> Pick(string title)
    {
        var file = await picker.PickAsync(new PickOptions { PickerTitle = title });
        if (file is null)
            return null;

        // Pickers can hand back a content URI (Android) or a security-scoped file (iOS): copy it locally.
        var directory = Path.Combine(fileSystem.CacheDirectory, "imports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.GetFileName(file.FileName));
        await using (var source = await file.OpenReadAsync())
        await using (var target = File.Create(path))
            await source.CopyToAsync(target);
        return path;
    }
}
