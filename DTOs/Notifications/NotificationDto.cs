namespace TimesheetApp.DTOs.Notifications;

public class NotificationDto
{
    public int Id { get; set; }
    public string? Message { get; set; }
    public int Importance { get; set; }
    public string? For { get; set; }
    public string? UserId { get; set; }
}
