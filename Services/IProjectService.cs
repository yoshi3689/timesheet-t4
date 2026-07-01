using Microsoft.AspNetCore.Identity;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public interface IProjectService
{
    IEnumerable<Project> GetProjectsForUser(string userId, bool isHrOrAdmin);
    Project? GetProjectById(int id);
    Project? GetProjectByIdForUser(int id, string userId, bool isHrOrAdmin);
    (bool valid, string? error) ValidateNewProject(CreateProjectViewModel input);
    Task CreateProjectAsync(CreateProjectViewModel input);
    Task<bool> VerifyProjectManagerAsync(int projectId, string userId);
    Task<byte[]> GenerateReportAsync(int projectId);
    Task<byte[]> GenerateWeekReportAsync(int projectId);
    Task<byte[]> GeneratePCBACAsync(int projectId);
    WpCostRollup CalculateWpCostRollup(
        List<Budget> budgets, List<ResponsibleEngineerEstimate> estimates,
        List<TimesheetRow> verifiedRows, List<LabourGrade> labourGrades);
    Task<List<ProjectEmployeeDto>> GetAllProjectEmployeesAsync(int projectId, string currentUserId);
    Task<bool> AssignAssistantProjectManagerAsync(int projectId, string employeeNumber, string currentPmId);
}
