namespace TimesheetApp.Models;

public class ProjectEmployeeDto
{
    public string Id { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public long EmployeeNumber { get; set; }
    public string JobTitle { get; set; } = null!;
    public string LabourGradeCode { get; set; } = null!;
}
