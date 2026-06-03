namespace TimesheetApp.DTOs.Employees;

public class UpdateEmployeeDto
{
    public string Id { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public long EmployeeNumber { get; set; }
    public double SickDays { get; set; }
    public double FlexTime { get; set; }
    public string? JobTitle { get; set; }
    public double Salary { get; set; }
    public string? LabourGradeCode { get; set; }
    public string? SupervisorId { get; set; }
    public string? TimesheetApproverId { get; set; }
}
