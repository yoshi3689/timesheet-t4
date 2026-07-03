namespace TimesheetApp.Models;

public class ResponsibleBudgetWpDto
{
    public string WorkPackageId { get; set; } = null!;
    public int ProjectId { get; set; }
    public string Title { get; set; } = null!;
    public bool IsBottomLevel { get; set; }
    public WpCostRollup Rollup { get; set; } = null!;
    public List<string> LabourCodes { get; set; } = new();
    public Dictionary<string, double> LatestEstimates { get; set; } = new();
    public Dictionary<string, double> LabourRates { get; set; } = new();
}

public class ResponsibleBudgetGroupDto
{
    public string RootWorkPackageId { get; set; } = null!;
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = null!;
    public string RootTitle { get; set; } = null!;
    public List<ResponsibleBudgetWpDto> WorkPackages { get; set; } = new();
}
