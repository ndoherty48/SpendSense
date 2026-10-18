using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using SpendSense.Common.Models.Enums;
using SpendSense.Common.Security;
using SpendSense.Common.Services;
using SpendSense.Components.Pages;
using SpendSense.Components.Pages.Data;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Components;

/// <summary>
/// The backup actions as the user reaches them: Preferences → Data, the backup password page, unlocking a backup
/// from another device, and the dashboard reminder. The database is a real encrypted file (ADR 0004).
/// </summary>
public class BackupTests : ComponentTest
{
    const string Password = "correct horse battery";

    public BackupTests() : base(encryptedDatabase: true) =>
        Services.GetRequiredService<BackupKeys>().SetPassword(Password).GetAwaiter().GetResult();

    BackupService Backups => Services.GetRequiredService<BackupService>();

    NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>As if the backup were made on another device: nothing about its password is remembered here.</summary>
    Task ForgetBackupPassword() => Secrets.Remove(BackupKeys.StoreName);

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
    public async Task Back_up_now_shares_an_encrypted_backup_and_records_the_date()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var page = Render<Settings>();

        await Row(page, "Back up now").ClickAsync(new());

        var shared = Assert.Single(FileExchange.Shared);
        Assert.EndsWith(".zip", shared.Path);
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
        var backup = Files.PathFor("saved.zip");
        File.Copy(await Backups.CreateBackup(), backup);
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1, description: "Added later");
        FileExchange.NextPick = backup;

        var dialogs = Render<MudDialogProvider>();
        var page = Render<Settings>();
        var restoring = page.InvokeAsync(() => Row(page, "Restore from backup").Click());

        // The confirmation lists the backup's contents and warns before anything changes.
        dialogs.WaitForAssertion(() => Assert.Contains("saved.zip", dialogs.Markup));
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
        var backup = Files.PathFor("saved.zip");
        File.Copy(await Backups.CreateBackup(), backup);
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1);
        FileExchange.NextPick = backup;

        var dialogs = Render<MudDialogProvider>();
        var page = Render<Settings>();
        var restoring = page.InvokeAsync(() => Row(page, "Restore from backup").Click());
        dialogs.WaitForAssertion(() => Assert.Contains("saved.zip", dialogs.Markup));

        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();
        await restoring;

        Assert.Equal(2, await Db.Transactions.CountAsync(Xunit.TestContext.Current.CancellationToken));
        Assert.Empty(SnackbarMessages);
    }

    [Fact]
    public async Task Back_up_now_without_a_password_goes_to_set_one_first()
    {
        await ForgetBackupPassword();
        var page = Render<Settings>();

        await Row(page, "Back up now").ClickAsync(new());

        Assert.Empty(FileExchange.Shared);
        Assert.EndsWith("/data/backup-password?then=backup", Navigation.Uri);
    }

    [Fact]
    public async Task Setting_a_password_shows_the_recovery_code_once_then_backs_up()
    {
        await ForgetBackupPassword();
        Navigation.NavigateTo("/data/backup-password?then=backup");
        var page = Render<BackupPassword>();

        page.Find("input[type=password]").Input("too short");
        page.FindAll("input[type=password]")[1].Input("too short");
        await page.Find("form").SubmitAsync();
        Assert.Contains("Use at least 10 characters.", page.Markup);

        page.FindAll("input[type=password]")[0].Input(Password);
        page.FindAll("input[type=password]")[1].Input("something else entirely");
        await page.Find("form").SubmitAsync();
        Assert.Contains("The passwords don't match.", page.Markup);

        page.FindAll("input[type=password]")[1].Input(Password);
        await page.Find("form").SubmitAsync();

        page.WaitForAssertion(() => Assert.Equal("Recovery code", page.Find("h1").TextContent));
        var code = page.Find(".code").TextContent;
        Assert.Matches("^[A-Z2-7]{4}(-[A-Z2-7]{4}){5}-[A-Z2-7]{2}$", code);
        await page.Find("button.copy").ClickAsync(new());
        Assert.Equal(code, Clipboard.Text);

        // Done needs the code saved first.
        await page.Find("form").SubmitAsync();
        Assert.Empty(FileExchange.Shared);
        Assert.Contains("Save your recovery code first", page.Markup);

        page.Find("input[type=checkbox]").Change(true);
        await page.Find("form").SubmitAsync();

        page.WaitForAssertion(() => Assert.Single(FileExchange.Shared));
        Assert.EndsWith("/settings", Navigation.Uri);
        Assert.True(await Backups.HasPassword());
    }

    [Fact]
    public async Task A_backup_from_another_device_asks_for_its_password_before_restoring()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, description: "In the backup");
        var backup = Files.PathFor("saved.zip");
        File.Copy(await Backups.CreateBackup(), backup);
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1, description: "Added later");
        await ForgetBackupPassword();
        FileExchange.NextPick = backup;

        var dialogs = Render<MudDialogProvider>();
        var page = Render<Settings>();
        var restoring = page.InvokeAsync(() => Row(page, "Restore from backup").Click());

        dialogs.WaitForAssertion(() => Assert.Contains("Enter the backup password", dialogs.Markup));
        dialogs.Find("input[type=password]").Input("not the password");
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Unlock").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("doesn't match this backup", dialogs.Markup));

        dialogs.Find("input[type=password]").Input(Password);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Unlock").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("This replaces everything on this device.", dialogs.Markup));

        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Restore").Click();
        await restoring;

        Assert.Equal(["In the backup"], await Db.Transactions.Select(t => t.Description).ToListAsync(ct));
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
