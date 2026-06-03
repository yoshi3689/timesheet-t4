namespace TimesheetApp.DTOs.Projects;

public class ProjectSummaryDto
{
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = "";
    public string? ProjectManagerId { get; set; }
    public string? ProjectManagerName { get; set; }
    public string? AssistantProjectManagerId { get; set; }
    public double TotalBudget { get; set; }
    public double ActualCost { get; set; }
    public bool IsClosed { get; set; }
}
