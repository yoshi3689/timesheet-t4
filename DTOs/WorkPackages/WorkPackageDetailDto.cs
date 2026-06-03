namespace TimesheetApp.DTOs.WorkPackages;

public class WorkPackageDetailDto
{
    public string WorkPackageId { get; set; } = "";
    public int? ProjectId { get; set; }
    public string? Title { get; set; }
    public string? ResponsibleUserId { get; set; }
    public string? ResponsibleUserName { get; set; }
    public string? ParentWorkPackageId { get; set; }
    public int ParentWorkPackageProjectId { get; set; }
    public bool IsBottomLevel { get; set; }
    public bool IsClosed { get; set; }
    public double ActualCost { get; set; }
    public double TotalBudget { get; set; }
    public double TotalRemaining { get; set; }
    public string? ProjectTitle { get; set; }
}
