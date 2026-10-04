using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Components.Pages;
using SpendSense.Components.Pages.Data;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Components;

/// <summary>The backup actions as the user reaches them: Preferences → Data, and the dashboard reminder.</summary>
public class BackupTests : ComponentTest
{
    BackupService Backups => Services.GetRequiredService<BackupService>();

    IEnumerable<string> SnackbarMessages => Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(s => s.Message);

    static AngleSharp.Dom.IElement Row(IRenderedComponent<Settings> page, string title) =>
        page.FindAll("button.row").Single(r => r.QuerySelector(".title")?.TextContent == title);

    [Fact]
    public void Preferences_says_when_you_have_never_backed_up()
    {
        var page = Render<Settings>();

        Assert.Equal("Never backed up", Row(page, "Back up now").QuerySelector(".subtitle")?.TextContent);
    }

    [Fact]
    public async Task Back_up_now_shares_a_database_file_and_records_the_date()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var page = Render<Settings>();

        await Row(page, "Back up now").ClickAsync(new());

        var shared = Assert.Single(FileExchange.Shared);
        Assert.EndsWith(".db", shared.Path);
        Assert.True(File.Exists(shared.Path));
        Assert.NotNull(Settings.LastBackupAt);
        page.WaitForAssertion(() => Assert.StartsWith("Last backup: ", Row(page, "Back up now").QuerySelector(".subtitle")?.TextContent));
    }

    [Fact]
    public async Task Export_shares_a_csv_file()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, description: "Coffee");
        var page = Render<Settings>();

        await Row(page, "Export transactions (CSV)").ClickAsync(new());

        var shared = Assert.Single(FileExchange.Shared);
        Assert.EndsWith(".csv", shared.Path);
        Assert.Contains("Coffee", await File.ReadAllTextAsync(shared.Path, Xunit.TestContext.Current.CancellationToken));
        Assert.Null(Settings.LastBackupAt); // an export isn't a backup
    }

    [Fact]
    public async Task Cancelling_the_file_picker_does_nothing()
    {
        FileExchange.NextPick = null;
        var page = Render<Settings>();

        await Row(page, "Restore from backup").ClickAsync(new());

        Assert.Empty(SnackbarMessages);
    }

    [Fact]
    public async Task Picking_a_file_that_is_not_a_backup_explains_why()
    {
        FileExchange.NextPick = Files.PathFor("photo.jpg");
        await File.WriteAllTextAsync(FileExchange.NextPick, "not a database", Xunit.TestContext.Current.CancellationToken);
        var page = Render<Settings>();

        await Row(page, "Restore from backup").ClickAsync(new());

        Assert.Contains("This file isn't a SpendSense backup.", SnackbarMessages);
    }

    [Fact]
    public async Task Restoring_shows_what_is_in_the_backup_then_replaces_the_data_and_reloads()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, description: "In the backup");
        var backup = Files.PathFor("saved.db");
        File.Copy(await Backups.CreateBackup(), backup);
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1, description: "Added later");
        FileExchange.NextPick = backup;

        var dialogs = Render<MudDialogProvider>();
        var page = Render<Settings>();
        var restoring = page.InvokeAsync(() => Row(page, "Restore from backup").Click());

        // The confirmation lists the backup's contents and warns before anything changes.
        dialogs.WaitForAssertion(() => Assert.Contains("saved.db", dialogs.Markup));
        Assert.Contains("This replaces everything on this device.", dialogs.Markup);
        Assert.Equal(2, await Db.Transactions.CountAsync(ct));

        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Restore").Click();
        await restoring;

        page.WaitForAssertion(() => Assert.Contains("Backup restored.", SnackbarMessages));
        Assert.Equal(["In the backup"], await Db.Transactions.Select(t => t.Description).ToListAsync(ct));
        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(navigation.BaseUri, navigation.Uri);
    }

    [Fact]
    public async Task Cancelling_the_confirmation_keeps_the_current_data()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var backup = Files.PathFor("saved.db");
        File.Copy(await Backups.CreateBackup(), backup);
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1);
        FileExchange.NextPick = backup;

        var dialogs = Render<MudDialogProvider>();
        var page = Render<Settings>();
        var restoring = page.InvokeAsync(() => Row(page, "Restore from backup").Click());
        dialogs.WaitForAssertion(() => Assert.Contains("saved.db", dialogs.Markup));

        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();
        await restoring;

        Assert.Equal(2, await Db.Transactions.CountAsync(Xunit.TestContext.Current.CancellationToken));
        Assert.Empty(SnackbarMessages);
    }

    [Fact]
    public void Reminder_explains_why_and_says_you_have_never_backed_up()
    {
        var cut = Render<BackupReminder>();

        Assert.Equal("Back up your data", cut.Find("h2").TextContent);
        Assert.Contains("You haven't backed up yet.", cut.Markup);
    }

    [Fact]
    public void Reminder_says_how_long_ago_the_last_backup_was()
    {
        Settings.LastBackupAt = DateTime.Now.AddDays(-45);

        Assert.Contains("Your last backup was 45 days ago.", Render<BackupReminder>().Markup);
    }

    [Fact]
    public async Task Reminder_back_up_now_shares_a_backup_and_hides()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var done = false;
        var cut = Render<BackupReminder>(p => p.Add(x => x.OnDone, () => done = true));

        await cut.Find("button.primary").ClickAsync(new());

        Assert.Single(FileExchange.Shared);
        Assert.True(done);
        Assert.False(await Backups.NeedsReminder());
    }

    [Fact]
    public async Task Reminder_not_now_snoozes_for_30_days()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var done = false;
        var cut = Render<BackupReminder>(p => p.Add(x => x.OnDone, () => done = true));

        cut.Find("button.secondary").Click();

        Assert.True(done);
        Assert.Empty(FileExchange.Shared);
        Assert.False(await Backups.NeedsReminder());
        Assert.True(await Backups.NeedsReminder(DateTime.Now.AddDays(BackupService.ReminderDays + 1)));
    }
}
