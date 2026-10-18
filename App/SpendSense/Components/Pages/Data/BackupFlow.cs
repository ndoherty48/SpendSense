using Microsoft.AspNetCore.Components;

using MudBlazor;

using SpendSense.Common.Services;

namespace SpendSense.Components.Pages.Data;

/// <summary>
/// The user-facing backup actions (share a backup, export CSV, restore a file), shared by Preferences, the
/// dashboard reminder and the recovery screen. Each returns whether it completed; failures are reported with a
/// snackbar, never thrown.
/// </summary>
public class BackupFlow(BackupService backups, IFileExchange files, IDialogService dialogs, ISnackbar snackbar, NavigationManager navigation)
{
    /// <summary>Sets or changes the backup password (ADR 0004).</summary>
    public const string PasswordPage = "/data/backup-password";

    /// <summary>
    /// Makes an encrypted backup and opens the share sheet so the user can save it somewhere safe. With no backup
    /// password yet, goes to set one up first; that page backs up when it's done.
    /// </summary>
    public async Task<bool> BackUp()
    {
        try
        {
            if (!await backups.HasPassword())
            {
                navigation.NavigateTo($"{PasswordPage}?then=backup");
                return false;
            }

            var path = await backups.CreateBackup();
            await files.Share(path, "Save your SpendSense backup");
            // The sheet can't tell us whether it was saved; closing it counts as backed up.
            backups.MarkBackedUp();
            return true;
        }
        catch (Exception ex)
        {
            snackbar.Add($"Couldn't create the backup: {ex.Message}", Severity.Error);
            return false;
        }
    }

    public async Task<bool> ExportCsv()
    {
        try
        {
            await files.Share(await backups.ExportTransactionsCsv(), "Export transactions");
            return true;
        }
        catch (Exception ex)
        {
            snackbar.Add($"Couldn't export transactions: {ex.Message}", Severity.Error);
            return false;
        }
    }

    /// <summary>
    /// Picks a backup, unlocks it if this device can't, shows what's in it, and on confirmation replaces this
    /// device's data with it. <paramref name="beforeRestore"/> runs after confirmation, just before the restore: the
    /// recovery screen uses it to set the locked database aside.
    /// </summary>
    public async Task<bool> Restore(Func<Task>? beforeRestore = null)
    {
        string? path;
        try
        {
            path = await files.Pick("Choose a SpendSense backup");
        }
        catch (Exception ex)
        {
            snackbar.Add($"Couldn't open the file: {ex.Message}", Severity.Error);
            return false;
        }
        if (path is null)
            return false;

        BackupInfo info;
        try
        {
            info = await backups.Inspect(path);
        }
        catch (Exception ex)
        {
            snackbar.Add($"Couldn't open the file: {ex.Message}", Severity.Error);
            return false;
        }

        if (info.IsLocked)
        {
            var unlock = await dialogs.ShowAsync<UnlockBackupDialog>("Unlock backup", new DialogParameters<UnlockBackupDialog>
            {
                { x => x.Path, path },
                { x => x.FileName, Path.GetFileName(path) }
            });
            if (await unlock.Result is not { Canceled: false, Data: BackupInfo unlocked })
                return false;
            info = unlocked;
        }

        if (!info.IsValid)
        {
            snackbar.Add(info.Problem ?? "This file can't be restored.", Severity.Error);
            return false;
        }

        var parameters = new DialogParameters<RestoreBackupDialog>
        {
            { x => x.Info, info },
            { x => x.FileName, Path.GetFileName(path) }
        };
        var dialog = await dialogs.ShowAsync<RestoreBackupDialog>("Restore backup", parameters);
        if (await dialog.Result is not { Canceled: false })
            return false;

        try
        {
            if (beforeRestore is not null)
                await beforeRestore();
            await backups.Restore(path, info);
        }
        catch (Exception ex)
        {
            snackbar.Add($"Couldn't restore the backup: {ex.Message}", Severity.Error);
            return false;
        }

        snackbar.Add("Backup restored.", Severity.Success);
        // Reload so every page reads the restored data from scratch.
        navigation.NavigateTo("/", forceLoad: true);
        return true;
    }
}
