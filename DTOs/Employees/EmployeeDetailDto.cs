namespace TimesheetApp.DTOs.Employees;

public class EmployeeDetailDto
{
    public string Id { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public long EmployeeNumber { get; set; }
    public string? JobTitle { get; set; }
    public string? LabourGradeCode { get; set; }
    public string? SupervisorId { get; set; }
    public string? TimesheetApproverId { get; set; }
    public string? Email { get; set; }
    public double SickDays { get; set; }
    public double FlexTime { get; set; }
    public double Overtime { get; set; }
    public double Salary { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool? TwoFactorPolicyOverride { get; set; }
    public bool TwoFactorEnabled { get; set; }
}
