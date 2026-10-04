namespace SpendSense.Common.Services;

/// <summary>Hands files to and from the user. Abstracted so pages can be tested without the OS sheets.</summary>
public interface IFileExchange
{
    /// <summary>Opens the share sheet for a file (Files, iCloud, Drive, email…). Returns when the sheet closes.</summary>
    Task Share(string path, string title);

    /// <summary>Lets the user pick a file and copies it to a local path, or returns null if they cancelled.</summary>
    Task<string?> Pick(string title);
}
