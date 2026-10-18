using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;

using Microsoft.Extensions.DependencyInjection;

using SpendSense.Common.Data;
using SpendSense.Common.Security;
using SpendSense.Common.Services;
using SpendSense.Components;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Components;

/// <summary>The app waits for the database to open, and a missing key shows the recovery screen (ADR 0004).</summary>
public class DatabaseGateTests : ComponentTest
{
    // The gate shows the recovery screen in the app's own layout, which needs the theme.
    public DatabaseGateTests() => Services.AddSingleton<ThemeService>();

    static RenderFragment App => builder => builder.AddMarkupContent(0, "<p class=\"app\">The app</p>");

    [Fact]
    public void A_new_install_opens_straight_into_the_app()
    {
        var gate = Render<DatabaseGate>(p => p.AddChildContent(App));

        gate.WaitForAssertion(() => Assert.NotNull(gate.Find("p.app")));
        Assert.True(Key.IsLoaded);
    }

    [Fact]
    public async Task Locked_data_shows_the_recovery_screen_and_start_fresh_opens_the_app()
    {
        // An encrypted database whose key this device doesn't have.
        Directory.CreateDirectory(DatabasePaths.Folder(Files));
        using (var locked = SqliteCipher.Open(DatabasePaths.Database(Files), System.Security.Cryptography.RandomNumberGenerator.GetBytes(DatabaseKey.Size)))
            SqliteCipher.Execute(locked, "CREATE TABLE Kept (Id INTEGER PRIMARY KEY);");

        var gate = Render<DatabaseGate>(p => p.AddChildContent(App));

        gate.WaitForAssertion(() => Assert.Equal("Your data is locked", gate.Find("h1").TextContent));
        Assert.Empty(gate.FindAll("p.app"));

        var startingFresh = gate.InvokeAsync(() => gate.FindAll("button.row").Single(b => b.TextContent.Contains("Start fresh")).Click());
        // MainLayout (inside the gate's layout) hosts the dialog provider.
        gate.WaitForAssertion(() => Assert.Contains("moved aside on this device, not deleted", gate.Markup));
        gate.FindAll("button").Single(b => b.TextContent.Trim() == "Start fresh" && !b.ClassList.Contains("row")).Click();
        await startingFresh;

        gate.WaitForAssertion(() => Assert.NotNull(gate.Find("p.app")));
        Assert.Single(Directory.GetDirectories(DatabasePaths.Locked(Files)));
        SqliteConnection.ClearAllPools();
    }
}
