using SpendSense.Common.Services;

namespace SpendSense.Tests.Unit;

/// <summary>When the dashboard suggests a backup (ADR 0003, decision 4).</summary>
public class BackupReminderTests
{
    static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0);

    [Fact]
    public void Never_backed_up_with_data_reminds()
    {
        Assert.True(BackupService.ShouldRemind(Now, lastBackup: null, snoozedUntil: null, hasData: true));
    }

    [Fact]
    public void Nothing_to_back_up_never_reminds()
    {
        Assert.False(BackupService.ShouldRemind(Now, lastBackup: null, snoozedUntil: null, hasData: false));
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, false)]
    [InlineData(31, true)]
    public void Reminds_once_the_last_backup_is_over_30_days_old(int daysAgo, bool expected)
    {
        Assert.Equal(expected, BackupService.ShouldRemind(Now, Now.AddDays(-daysAgo), snoozedUntil: null, hasData: true));
    }

    [Fact]
    public void Snoozing_hides_it_until_the_snooze_ends()
    {
        Assert.False(BackupService.ShouldRemind(Now, null, snoozedUntil: Now.AddDays(1), hasData: true));
        Assert.True(BackupService.ShouldRemind(Now, null, snoozedUntil: Now.AddDays(-1), hasData: true));
    }
}
