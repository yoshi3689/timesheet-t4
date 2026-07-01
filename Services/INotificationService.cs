using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public interface INotificationService
{
    List<Notification> GetUserNotifications(string userId);
    Task DismissNotificationAsync(int notificationId, string userId);
    void AddNotification(string userId, string message, string forField, int importance, string? targetUrl = null);
    bool NotificationExistsFor(string userId, string forField);
}
