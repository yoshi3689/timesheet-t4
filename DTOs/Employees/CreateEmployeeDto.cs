using System.ComponentModel.DataAnnotations;

namespace TimesheetApp.DTOs.Employees;

public class CreateEmployeeDto
{
    [Required]
    public string FirstName { get; set; } = "";
    [Required]
    public string LastName { get; set; } = "";
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required]
    public long EmployeeNumber { get; set; }
    [Required]
    public string JobTitle { get; set; } = "";
    [Required]
    public string LabourGradeCode { get; set; } = "";
    public string? SupervisorId { get; set; }
    public string? TimesheetApproverId { get; set; }
    public List<string> Roles { get; set; } = ["Employee"];
    public bool HasTempPassword { get; set; } = true;
}
