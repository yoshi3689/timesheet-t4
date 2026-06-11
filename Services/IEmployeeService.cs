using Microsoft.AspNetCore.Identity;
using TimesheetApp.DTOs.Employees;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public interface IEmployeeService
{
    Task<List<ApplicationUser>> GetAllEmployeesPaginatedAsync(int page, int pageSize);
    int GetTotalEmployeeCount();
    Task<ApplicationUser?> GetEmployeeDetailsAsync(string id);
    Task<ApplicationUser?> FindEmployeeAsync(string id);
    Task<IList<ApplicationUser>> GetSupervisorsAsync();
    IEnumerable<object> GetTimesheetApprovers(string supervisorId);
    Task UpdateEmployeeAsync(ApplicationUser existing, UpdateEmployeeDto dto, bool isAdminOrHR, UserManager<ApplicationUser> userManager);
    bool EmployeeExists(string id);
    ApplicationUser? GetCurrentUserWithSupervisedUsers(string userName);
    List<Project> GetAllProjectsWithEmployees();
    List<ApplicationUser> GetAvailableUsersForProject(int projectId, string supervisorId, string currentUserId, bool isAdmin);
    void AddEmployeesToProject(List<EmployeeProject> employeeProjects);
    void AssignTimesheetApprover(ApplicationUser supervisor, string futureApproverId);
    List<LabourGrade> GetCurrentYearLabourGrades();
}
