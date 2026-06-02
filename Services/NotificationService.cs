using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Notification> GetUserNotifications(string userId)
    {
        return _context.Notifications.Where(c => c.UserId == userId).ToList();
    }

    public async Task DismissNotificationAsync(int notificationId, string userId)
    {
        var noti = await _context.Notifications
            .Where(c => c.Id == notificationId && c.UserId == userId)
            .FirstOrDefaultAsync();
        if (noti != null)
        {
            _context.Remove(noti);
            await _context.SaveChangesAsync();
        }
    }

    public void AddNotification(string userId, string message, string forField, int importance)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = message,
            For = forField,
            Importance = importance
        });
    }

    public bool NotificationExistsFor(string forField)
    {
        return _context.Notifications.Any(n => n.For == forField);
    }
}
