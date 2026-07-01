using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public interface IWorkPackageService
{
    Task<List<WorkPackage>> GetResponsibleWorkPackagesAsync(string userId);
    Task<List<WorkPackage>> GetAssignedWorkPackagesAsync(string userId);
    Task<WorkPackage?> GetWorkPackageDetailsAsync(string id);
    LowestWorkPackageBAndEViewModel GetEditModel(string wpId, int projectId);
    void CreateBudgetsAndEstimates(LowestWorkPackageBAndEViewModel input);
    List<WorkPackage> GetProjectWorkPackagesTree(int projectId);
    List<WorkPackage> CalculateTotalMoney(List<WorkPackage> wps, List<Budget> budgets);
    (bool valid, string? error) ValidateNewWorkPackage(WorkPackageViewModel p, int projectId);
    WorkPackage CreateChildWorkPackage(WorkPackageViewModel p, int projectId);
    List<Budget> GetBudgetDetails(string workPackageId, int projectId);
    void CloseWorkPackage(string workPackageId, int projectId);
    void UpdateWorkPackage(string workPackageId, int projectId, string title, string? responsibleUserId);
    void CloseProject(int projectId);
    object GetWPEmployees(string workPackageId, int projectId);
    object GetCandidateEmployees(string workPackageId, int projectId);
    object GetAssignedEmployees(string workPackageId, int projectId);
    bool IsEmployeeAssignedToWP(string userId, string workPackageId, int projectId);
    object AssignEmployees(List<EmployeeWorkPackage> ewps);
    string? AssignResponsibleEngineer(EmployeeWorkPackage ewp);
    List<Budget> GetSplitBudgets(string workPackageId, int projectId);
    List<Budget> GetProjectBudgets(int projectId);
    List<LabourGrade> GetCurrentYearLabourGrades();
    Task<List<ResponsibleBudgetGroupDto>> GetResponsibleSubtreeWithBudgetAsync(string userId);
    WorkPackage? GetWorkPackage(string workPackageId, int projectId);
    void SubmitWeeklyEstimate(string wpProjectId, List<(string LabourCode, double EstimatedCost)> entries);
}
