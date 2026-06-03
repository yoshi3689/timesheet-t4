namespace TimesheetApp.DTOs.Timesheets;

public class TimesheetRowDto
{
    public int TimesheetRowId { get; set; }
    public int TimesheetId { get; set; }
    public string? WorkPackageId { get; set; }
    public int? WorkPackageProjectId { get; set; }
    public string? Notes { get; set; }
    public long PackedHours { get; set; }
    public string? OriginalLabourCode { get; set; }
    public bool WorkPackageIsClosed { get; set; }
}
