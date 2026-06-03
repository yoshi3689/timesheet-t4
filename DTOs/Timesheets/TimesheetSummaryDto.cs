namespace TimesheetApp.DTOs.Timesheets;

public class TimesheetSummaryDto
{
    public int TimesheetId { get; set; }
    public DateOnly? EndDate { get; set; }
    public double TotalHours { get; set; }
    public double FlexHours { get; set; }
    public double Overtime { get; set; }
    public string? UserId { get; set; }
    public string? UserFirstName { get; set; }
    public string? UserLastName { get; set; }
    public string? TimesheetApproverId { get; set; }
    public string? ApproverNotes { get; set; }
    public bool CurrentlySelected { get; set; }
}
