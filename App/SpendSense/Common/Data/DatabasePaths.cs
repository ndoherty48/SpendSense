namespace SpendSense.Common.Data;

/// <summary>Where the app's data lives under <see cref="IFileSystem.AppDataDirectory"/>.</summary>
public static class DatabasePaths
{
    public static string Folder(IFileSystem fileSystem) => Path.Join(fileSystem.AppDataDirectory, "SpendSense");

    public static string Database(IFileSystem fileSystem) => Path.Join(Folder(fileSystem), "SpendSense.db");

    /// <summary>Pre-restore safety copies (ADR 0003).</summary>
    public static string SafetyCopies(IFileSystem fileSystem) => Path.Join(Folder(fileSystem), "Backups");

    /// <summary>Databases set aside by "Start fresh" because their key was missing (ADR 0004).</summary>
    public static string Locked(IFileSystem fileSystem) => Path.Join(Folder(fileSystem), "Locked");

    /// <summary>The database file and its SQLite sidecar files.</summary>
    public static IEnumerable<string> WithSidecars(string database) => [database, database + "-wal", database + "-shm", database + "-journal"];
}
