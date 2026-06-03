namespace TimesheetApp.DTOs.Employees;

public class EmployeeSummaryDto
{
    public string Id { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public long EmployeeNumber { get; set; }
    public string? JobTitle { get; set; }
    public string? LabourGradeCode { get; set; }
    public string? SupervisorId { get; set; }
    public string? TimesheetApproverId { get; set; }
}
