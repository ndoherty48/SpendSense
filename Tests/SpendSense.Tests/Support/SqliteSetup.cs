using System.Runtime.CompilerServices;

namespace SpendSense.Tests.Support;

static class SqliteSetup
{
    /// <summary>
    /// The app uses the .Sqlite.Core provider, which has no bundled engine. Point SQLitePCLRaw at SQLite3 Multiple
    /// Ciphers before any test opens a connection, as <c>AddSpendSenseDb</c> does in the app.
    /// </summary>
    [ModuleInitializer]
    internal static void Init() => SQLitePCL.Batteries_V2.Init();
}
