namespace TimesheetApp.DTOs.WorkPackages;

public class WorkPackageSummaryDto
{
    public string WorkPackageId { get; set; } = "";
    public int? ProjectId { get; set; }
    public string? Title { get; set; }
    public string? ResponsibleUserId { get; set; }
    public string? ResponsibleUserName { get; set; }
    public string? ParentWorkPackageId { get; set; }
    public bool IsBottomLevel { get; set; }
    public bool IsClosed { get; set; }
    public double TotalBudget { get; set; }
    public double TotalRemaining { get; set; }
}
