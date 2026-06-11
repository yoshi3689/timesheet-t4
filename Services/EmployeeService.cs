using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.DTOs.Employees;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public class EmployeeService : IEmployeeService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmployeeService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<ApplicationUser>> GetAllEmployeesPaginatedAsync(int page, int pageSize)
    {
        int skip = (page - 1) * pageSize;
        return await _context.Users
            .Include(a => a.Supervisor)
            .Include(a => a.TimesheetApprover)
            .OrderBy(a => a.FirstName)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
    }

    public int GetTotalEmployeeCount()
    {
        return _context.Users
            .Include(a => a.Supervisor)
            .Include(a => a.TimesheetApprover)
            .OrderBy(a => a.FirstName)
            .Count();
    }

    public async Task<ApplicationUser?> GetEmployeeDetailsAsync(string id)
    {
        return await _context.Users
            .Include(a => a.Supervisor)
            .Include(a => a.TimesheetApprover)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<ApplicationUser?> FindEmployeeAsync(string id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<IList<ApplicationUser>> GetSupervisorsAsync()
    {
        return await _userManager.GetUsersInRoleAsync("Supervisor");
    }

    public IEnumerable<object> GetTimesheetApprovers(string supervisorId)
    {
        return _context.Users
            .Where(c => c.SupervisorId == supervisorId || c.Id == supervisorId)
            .Select(c => new
            {
                Name = c.FirstName + " " + c.LastName,
                Id = c.Id
            })
            .ToList();
    }

    public async Task UpdateEmployeeAsync(ApplicationUser existing, UpdateEmployeeDto dto, bool isAdminOrHR, UserManager<ApplicationUser> userManager)
    {
        existing.FirstName = dto.FirstName ?? existing.FirstName;
        existing.LastName = dto.LastName ?? existing.LastName;
        existing.JobTitle = dto.JobTitle ?? existing.JobTitle;

        if (isAdminOrHR)
        {
            existing.EmployeeNumber = dto.EmployeeNumber;
            existing.SickDays = dto.SickDays;
            existing.FlexTime = dto.FlexTime;
            existing.Salary = dto.Salary;
            existing.LabourGradeCode = dto.LabourGradeCode ?? existing.LabourGradeCode;
        }

        if (dto.SupervisorId != null &&
            await userManager.IsInRoleAsync(await userManager.FindByIdAsync(dto.SupervisorId) ?? new ApplicationUser(), "Supervisor") &&
            existing.Id != dto.SupervisorId)
        {
            existing.SupervisorId = dto.SupervisorId;
        }
        var approver = dto.TimesheetApproverId != null ? await userManager.FindByIdAsync(dto.TimesheetApproverId) : null;
        if (approver != null && dto.TimesheetApproverId != existing.Id &&
            (approver.SupervisorId == existing.SupervisorId || approver.Id == existing.SupervisorId))
        {
            existing.TimesheetApproverId = dto.TimesheetApproverId;
        }

        _context.Update(existing);
        await _context.SaveChangesAsync();
    }

    public bool EmployeeExists(string id)
    {
        return (_context.Users?.Any(e => e.Id == id)).GetValueOrDefault();
    }

    public ApplicationUser? GetCurrentUserWithSupervisedUsers(string userName)
    {
        return _context.Users
            .Where(c => c.UserName == userName)
            .Include(c => c.SupervisedUsers)
            .FirstOrDefault();
    }

    public List<Project> GetAllProjectsWithEmployees()
    {
        return _context.Projects
            .Where(c => c.ProjectId != 010)
            .Include(p => p.EmployeeProjects).ThenInclude(c => c.User)
            .Include(p => p.ProjectManager)
            .Include(p => p.AssistantProjectManager)
            .ToList();
    }

    public List<ApplicationUser> GetAvailableUsersForProject(int projectId, string supervisorId, string currentUserId, bool isAdmin)
    {
        var usersInProject = _context.EmployeeProjects
            .Where(ep => ep.ProjectId == projectId)
            .Select(ep => ep.UserId)
            .ToList();

        var usersAvailable = _context.Users
            .Where(u => u.SupervisorId == supervisorId && !usersInProject.Contains(u.Id))
            .ToList();

        if (isAdmin && !usersInProject.Contains(currentUserId))
        {
            var currentUser = _context.Users.Find(currentUserId);
            if (currentUser != null)
                usersAvailable.Add(currentUser);
        }
        return usersAvailable;
    }

    public void AddEmployeesToProject(List<EmployeeProject> employeeProjects)
    {
        foreach (var ep in employeeProjects)
        {
            _context.Add(ep);
        }
        _context.SaveChanges();
    }

    public void AssignTimesheetApprover(ApplicationUser supervisor, string futureApproverId)
    {
        var futureTSA = _context.Users.Find(futureApproverId);
        if (futureTSA == null)
            return;

        foreach (var s in supervisor.SupervisedUsers)
        {
            s.TimesheetApproverId = futureApproverId;
        }
        futureTSA.TimesheetApproverId = supervisor.Id;
        _context.SaveChanges();
    }

    public List<LabourGrade> GetCurrentYearLabourGrades()
    {
        return _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
    }
}
