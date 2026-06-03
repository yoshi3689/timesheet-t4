namespace TimesheetApp.DTOs.Projects;

public class CreateProjectDto
{
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = "";
    public string ProjectManagerId { get; set; } = "";
    public List<BudgetInputDto> Budgets { get; set; } = new();
}
