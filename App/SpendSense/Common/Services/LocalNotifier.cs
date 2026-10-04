using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace SpendSense.Common.Services;

/// <summary>Shows notifications on the device with Plugin.LocalNotification.</summary>
public class LocalNotifier : INotifier
{
    public async Task Show(string title, string description)
    {
        var request = new NotificationRequest
        {
            NotificationId = title.GetHashCode(),
            Title = title,
            Description = description
        };
        await LocalNotificationCenter.Current.Show(request);
    }
}
