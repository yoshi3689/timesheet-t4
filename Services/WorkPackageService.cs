using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public class WorkPackageService : IWorkPackageService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public WorkPackageService(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<List<WorkPackage>> GetResponsibleWorkPackagesAsync(string userId)
    {
        return await _context.WorkPackages
            .Where(wp => wp.ResponsibleUserId == userId && wp.IsBottomLevel)
            .Include(w => w.Project)
            .ToListAsync();
    }

    public async Task<WorkPackage?> GetWorkPackageDetailsAsync(string id)
    {
        return await _context.WorkPackages
            .Include(w => w.ParentWorkPackage)
            .Include(w => w.Project)
            .Include(w => w.ResponsibleUser)
            .FirstOrDefaultAsync(m => m.WorkPackageId == id);
    }

    public LowestWorkPackageBAndEViewModel GetEditModel(string wpId, int projectId)
    {
        int labourGradeCount = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).Count();
        List<Budget> budgets = _context.Budgets
            .Where(b => b.WPProjectId == projectId + "~" + wpId && b.isREBudget == true)
            .AsEnumerable()
            .TakeLast(labourGradeCount)
            .ToList();

        if (budgets.Count == 0)
        {
            budgets = _context.Budgets
                .Where(b => b.WPProjectId == projectId + "~" + wpId && b.isREBudget == false)
                .AsEnumerable()
                .TakeLast(labourGradeCount)
                .ToList();
        }

        List<ResponsibleEngineerEstimate> estimates = _context.ResponsibleEngineerEstimates
            .Where(ree => ree.WPProjectId == projectId + "~" + wpId)
            .OrderByDescending(c => c.Date)
            .Take(labourGradeCount)
            .ToList();

        int offset = (7 - (int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday) % 7;
        DateTime nextFriday = DateTime.Today.AddDays(offset);
        bool shouldMakeWE = estimates.Count == 0 || DateOnly.FromDateTime(nextFriday) != estimates[0].Date;
        estimates.Clear();

        var lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
        for (int i = 0; i < labourGradeCount; i++)
        {
            budgets[i].Days = 0;
            budgets[i].People = 0;
            estimates.Add(new ResponsibleEngineerEstimate
            {
                WPProjectId = projectId + "~" + wpId,
                LabourCode = budgets[i].LabourCode,
                Date = null,
                EstimatedCost = 0,
            });
        }

        foreach (var item in budgets)
        {
            item.Rate = lgs.Where(c => c.LabourCode == item.LabourCode).First().Rate;
        }

        return new LowestWorkPackageBAndEViewModel
        {
            budgets = budgets,
            estimates = shouldMakeWE ? estimates : null,
        };
    }

    public void CreateBudgetsAndEstimates(LowestWorkPackageBAndEViewModel input)
    {
        if (input.budgets != null)
        {
            Budget? parentB = null;
            List<Budget> parentBudgets = _context.Budgets
                .Where(c => c.WPProjectId == input.budgets[0].WPProjectId)
                .ToList();
            foreach (var budget in input.budgets)
            {
                Budget newBudget = new Budget
                {
                    WPProjectId = input.budgets[0].WPProjectId,
                    People = budget.People,
                    Days = budget.Days,
                    LabourCode = budget.LabourCode,
                    UnallocatedDays = budget.UnallocatedDays,
                    UnallocatedPeople = budget.UnallocatedPeople,
                    isREBudget = true
                };
                _context.Budgets!.Add(newBudget);
                parentB = parentBudgets.Where(c => c.LabourCode == budget.LabourCode).First();
                if (parentB != null)
                {
                    parentB.UnallocatedDays -= newBudget.UnallocatedDays;
                    parentB.UnallocatedPeople -= newBudget.UnallocatedPeople;
                }
            }
        }

        if (input.estimates != null)
        {
            foreach (var estimate in input.estimates)
            {
                int offset = (7 - (int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday) % 7;
                DateTime nextFriday = DateTime.Today.AddDays(offset);
                ResponsibleEngineerEstimate re = new ResponsibleEngineerEstimate
                {
                    WPProjectId = estimate.WPProjectId,
                    LabourCode = estimate.LabourCode,
                    Date = DateOnly.FromDateTime(nextFriday),
                    EstimatedCost = estimate.EstimatedCost,
                };
                _context.ResponsibleEngineerEstimates!.Add(re);
            }
            _context.SaveChanges();
        }
    }

    public List<WorkPackage> GetProjectWorkPackagesTree(int projectId)
    {
        var workpackages = _context.WorkPackages!
            .Where(c => c.ProjectId == projectId)
            .Include(c => c.ResponsibleUser)
            .Include(c => c.ParentWorkPackage)
            .Include(c => c.ChildWorkPackages)
            .Include(c => c.Project);

        var top = workpackages.FirstOrDefault(c => c.ParentWorkPackage == null)!;
        return FindAllChildren(top);
    }

    public List<WorkPackage> CalculateTotalMoney(List<WorkPackage> wps, List<Budget> budgets)
    {
        List<LabourGrade> lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
        foreach (var wp in wps)
        {
            double total = 0;
            double remaining = 0;
            foreach (var lg in lgs)
            {
                var budget = budgets.Where(c => c.WPProjectId == (wp.ProjectId + "~" + wp.WorkPackageId) && c.LabourCode == lg.LabourCode).First();
                total += budget.BudgetAmount * lg.Rate;
                remaining += budget.UnallocatedDays * budget.UnallocatedPeople * lg.Rate;
            }
            wp.TotalBudget = total;
            wp.TotalRemaining = remaining;
        }
        return wps;
    }

    public (bool valid, string? error) ValidateNewWorkPackage(WorkPackageViewModel p, int projectId)
    {
        if (p.WorkPackage.WorkPackageId.Length != 1)
        {
            return (false, "Work Package ID must be 1 character longer");
        }

        string newWPID = (p.WorkPackage!.ParentWorkPackageId == "0")
            ? p.WorkPackage.WorkPackageId
            : p.WorkPackage!.ParentWorkPackageId + p.WorkPackage!.WorkPackageId;

        var exists = _context.WorkPackages!
            .Any(c => c.ProjectId == projectId && c.WorkPackageId == newWPID);

        if (exists)
        {
            return (false, "Work Package ID must be unique for this project");
        }

        return (true, null);
    }

    public WorkPackage CreateChildWorkPackage(WorkPackageViewModel p, int projectId)
    {
        string newWPID = (p.WorkPackage!.ParentWorkPackageId == "0")
            ? p.WorkPackage.WorkPackageId
            : p.WorkPackage!.ParentWorkPackageId + p.WorkPackage!.WorkPackageId;

        var parent = _context.WorkPackages!.FirstOrDefault(c => c.ProjectId == projectId && c.WorkPackageId == p.WorkPackage!.ParentWorkPackageId);
        if (parent != null)
        {
            parent.IsBottomLevel = false;
        }

        var newChild = new WorkPackage
        {
            WorkPackageId = newWPID,
            ProjectId = projectId,
            ParentWorkPackageId = p.WorkPackage!.ParentWorkPackageId,
            ParentWorkPackageProjectId = projectId,
            IsBottomLevel = true,
            IsClosed = false,
            Title = p.WorkPackage.Title
        };

        if (p.budgets != null)
        {
            Budget? parentB = null;
            List<Budget> parentBudgets = _context.Budgets.Where(c => c.WPProjectId == projectId + "~" + newChild.ParentWorkPackageId).ToList();
            foreach (var budget in p.budgets)
            {
                Budget newBudget = new Budget
                {
                    WPProjectId = projectId + "~" + newChild.WorkPackageId,
                    People = budget.People,
                    Days = budget.Days,
                    LabourCode = budget.LabourCode,
                    UnallocatedDays = budget.Days,
                    UnallocatedPeople = budget.People
                };
                _context.Budgets!.Add(newBudget);
                parentB = parentBudgets.FirstOrDefault(c => c.LabourCode == budget.LabourCode);
                if (parentB != null)
                {
                    parentB.UnallocatedDays -= newBudget.UnallocatedDays;
                    parentB.UnallocatedPeople -= newBudget.UnallocatedPeople;
                }
            }
        }

        _context.WorkPackages!.Add(newChild);
        _context.SaveChanges();

        newChild.ParentWorkPackage = null;
        if (newChild.ResponsibleUser == null)
        {
            newChild.ResponsibleUser = new ApplicationUser
            {
                FirstName = null,
                LastName = null
            };
        }

        List<Budget> budgets = _context.Budgets.Where(c => c.WPProjectId == (newChild.ProjectId + "~" + newChild.WorkPackageId)).ToList();
        List<LabourGrade> lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
        double total = 0;
        double remaining = 0;
        foreach (var lg in lgs)
        {
            var budget = budgets.FirstOrDefault(c => c.LabourCode == lg.LabourCode);
            if (budget == null) continue;
            total += budget.BudgetAmount * lg.Rate;
            remaining += budget.BudgetAmount * lg.Rate;
        }
        newChild.TotalBudget = total;
        newChild.TotalRemaining = remaining;
        newChild.Project = null;

        return newChild;
    }

    public List<Budget> GetBudgetDetails(string workPackageId, int projectId)
    {
        var budgets = _context.Budgets
            .Where(c => c.WPProjectId == (projectId + "~" + workPackageId))
            .ToList();
        var lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
        foreach (var budget in budgets)
        {
            budget.Rate = lgs.Where(c => c.LabourCode == budget.LabourCode).First().Rate;
        }
        return budgets;
    }

    public void CloseWorkPackage(string workPackageId, int projectId)
    {
        var closingwp = _context.WorkPackages
            .Include(w => w.ChildWorkPackages)
            .SingleOrDefault(w => w.WorkPackageId == workPackageId && w.ProjectId == projectId);

        if (closingwp != null)
        {
            if (closingwp.ChildWorkPackages.Count != 0)
            {
                foreach (var child in closingwp.ChildWorkPackages)
                {
                    CloseWorkPackage(child.WorkPackageId, projectId);
                }
            }
            closingwp.IsClosed = true;
        }
        _context.SaveChanges();
    }

    public void CloseProject(int projectId)
    {
        var project = _context.Projects.Find(projectId);
        if (project == null) return;
        project.IsClosed = true;
        foreach (var wp in _context.WorkPackages.Where(c => c.ProjectId == projectId).ToList())
        {
            wp.IsClosed = true;
        }
        _context.SaveChanges();
    }

    public object GetWPEmployees(string workPackageId, int projectId)
    {
        var userIdsInLLWP = _context.EmployeeWorkPackages!
            .Where(ewp => ewp.WorkPackageId == workPackageId && ewp.WorkPackageProjectId == projectId)
            .Select(filtered => filtered.UserId)
            .ToList();

        var budgets = _context.Budgets
            .Where(c => c.WPProjectId == (projectId + "~" + workPackageId))
            .ToList();

        var emp = _context.EmployeeProjects!
            .Where(ep => ep.ProjectId == projectId)
            .Select(e => new EmployeeWorkPackageViewModel
            {
                Employee = e.User!,
                Assigned = userIdsInLLWP.Contains(e.UserId)
            })
            .ToList();

        foreach (var employee in emp)
        {
            var matchingBudget = budgets.FirstOrDefault(b => b.LabourCode == employee.Employee.LabourGradeCode);
            if (matchingBudget != null && employee.Assigned)
            {
                matchingBudget.People--;
            }
        }

        var result = new List<object>();
        result.Add(emp.Select(e => new
        {
            e.Employee.Id,
            FirstName = e.Employee.FirstName ?? string.Empty,
            LastName = e.Employee.LastName ?? string.Empty,
            JobTitle = e.Employee.JobTitle ?? string.Empty,
            e.Assigned,
            LabourCode = e.Employee.LabourGradeCode ?? string.Empty
        }));
        result.Add(budgets.Select(c => new { c.LabourCode, c.People }));
        return result;
    }

    public object GetCandidateEmployees(string workPackageId, int projectId)
    {
        var wp = _context.WorkPackages
            .Where(c => c.WorkPackageId == workPackageId && c.ProjectId == projectId)
            .FirstOrDefault();

        if (wp == null) return null!;

        var userIdsInLLWP = _context.EmployeeWorkPackages!
            .Where(ewp => ewp.WorkPackageId == workPackageId && ewp.WorkPackageProjectId == projectId)
            .Select(e => e.User);

        foreach (var user in userIdsInLLWP)
        {
            if (user != null && user.Id == wp.ResponsibleUserId)
            {
                user.Selected = true;
                break;
            }
        }

        return userIdsInLLWP.Select(e => new
        {
            e!.Id,
            FirstName = e.FirstName!,
            LastName = e.LastName!,
            JobTitle = e.JobTitle!,
            e.Selected
        });
    }

    public object GetAssignedEmployees(string workPackageId, int projectId)
    {
        return _context.EmployeeWorkPackages!
            .Where(ewp => ewp.WorkPackageId == workPackageId && ewp.WorkPackageProjectId == projectId)
            .Select(e => e.User)
            .Select(e => new { e!.Id, e.FirstName, e.LastName, e.JobTitle });
    }

    public object AssignEmployees(List<EmployeeWorkPackage> ewps)
    {
        var workPackageId = ewps[0].WorkPackageId;
        var workPackageProjectId = ewps[0].WorkPackageProjectId;

        var oldWp = _context.WorkPackages
            .Where(c => ewps.Select(s => s.WorkPackageId).Contains(c.WorkPackageId) && c.IsBottomLevel == false)
            .ToList();
        if (oldWp.Any()) return "Error";

        var currentWp = _context.WorkPackages
            .Where(c => c.WorkPackageId == workPackageId)
            .Include(c => c.Project)
            .First();
        if (currentWp.IsClosed) return null!;

        var removedEmployeeIds = _context.EmployeeWorkPackages
            .Where(c => c.WorkPackageId == workPackageId && c.WorkPackageProjectId == workPackageProjectId)
            .Select(c => c.UserId)
            .ToList();

        _context.EmployeeWorkPackages.RemoveRange(
            _context.EmployeeWorkPackages.Where(c => c.WorkPackageId == workPackageId && c.WorkPackageProjectId == workPackageProjectId));

        var addedEmployeeIds = ewps.Where(e => e.UserId != null).Select(e => e.UserId).ToList();
        var notifiedAddedEmployeeIds = addedEmployeeIds.Except(removedEmployeeIds).ToList();
        var notifiedRemovedEmployeeIds = removedEmployeeIds.Except(addedEmployeeIds).ToList();

        _context.EmployeeWorkPackages.AddRange(ewps.Where(e => e.UserId != null));

        var workPackageString = workPackageProjectId + "~" + workPackageId;

        foreach (var notifiedEmployeeId in notifiedAddedEmployeeIds)
        {
            if (_context.Users.Any(e => e.Id == notifiedEmployeeId) && !_notificationService.NotificationExistsFor(workPackageString + " Add"))
            {
                _notificationService.AddNotification(notifiedEmployeeId!, "You have been added to the work package " + currentWp.Title + " in the project " + currentWp.Project!.ProjectTitle, workPackageString + " Add", 1);
            }
        }

        foreach (var notifiedEmployeeId in notifiedRemovedEmployeeIds)
        {
            if (_context.Users.Any(e => e.Id == notifiedEmployeeId) && !_notificationService.NotificationExistsFor(workPackageString + " Remove"))
            {
                _notificationService.AddNotification(notifiedEmployeeId!, "You have been removed from the work package " + currentWp.Title + " in the project " + currentWp.Project!.ProjectTitle, workPackageString + " Remove", 2);
            }
        }

        _context.SaveChanges();

        return _context.Users
            .Where(c => addedEmployeeIds.Contains(c.Id))
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.JobTitle });
    }

    public string? AssignResponsibleEngineer(EmployeeWorkPackage ewp)
    {
        var llwp = _context.WorkPackages.Find(ewp.WorkPackageId, ewp.WorkPackageProjectId);
        if (llwp == null) return null;
        if (llwp.IsClosed) return null;

        var oldRE = llwp.ResponsibleUserId;
        llwp.ResponsibleUserId = ewp.UserId;

        var user = _context.Users.Where(c => c.Id == ewp.UserId).FirstOrDefault();
        if (user == null) return null;

        var fullEwp = _context.EmployeeWorkPackages
            .Where(c => c.UserId == ewp.UserId && c.WorkPackageId == ewp.WorkPackageId)
            .Include(c => c.WorkPackage)
            .Include(c => c.WorkPackage!.Project)
            .First();

        var workPackageString = fullEwp.WorkPackageProjectId + "~" + fullEwp.WorkPackageId;

        if (oldRE != null)
        {
            _notificationService.AddNotification(oldRE, "You have been removed from the work package " + fullEwp.WorkPackage!.Title + " in the project " + fullEwp.WorkPackage.Project!.ProjectTitle + " as a Responsible Engineer.", workPackageString + " Remove", 2);
        }
        _notificationService.AddNotification(ewp.UserId!, "You have been added to the work package " + fullEwp.WorkPackage!.Title + " in the project " + fullEwp.WorkPackage.Project!.ProjectTitle + " as a Responsible Engineer.", workPackageString + " Add", 1);

        _context.SaveChanges();
        return user.FirstName + " " + user.LastName;
    }

    public List<Budget> GetSplitBudgets(string workPackageId, int projectId)
    {
        var projectBudget = _context.Budgets
            .Where(c => c.WPProjectId == projectId + "~" + workPackageId)
            .ToList();

        List<Budget> emptyBudgets = new List<Budget>();
        foreach (var item in _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList())
        {
            emptyBudgets.Add(new Budget
            {
                LabourCode = item.LabourCode,
                isREBudget = false,
                Rate = item.Rate,
                UnallocatedDays = projectBudget.Where(c => c.LabourCode == item.LabourCode).First().UnallocatedDays,
                UnallocatedPeople = projectBudget.Where(c => c.LabourCode == item.LabourCode).First().UnallocatedPeople
            });
        }
        return emptyBudgets;
    }

    public List<Budget> GetProjectBudgets(int projectId)
    {
        return _context.Budgets
            .Where(c => c.WPProjectId.StartsWith(projectId + "~"))
            .ToList();
    }

    public List<LabourGrade> GetCurrentYearLabourGrades()
    {
        return _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
    }

    private List<WorkPackage> FindAllChildren(WorkPackage top)
    {
        List<WorkPackage> wps = new List<WorkPackage>();
        wps.Add(top);
        if (top.ChildWorkPackages == null || top.ChildWorkPackages.Count() == 0)
        {
            top = _context.WorkPackages!
                .Include(c => c.ChildWorkPackages)
                .Include(c => c.ResponsibleUser)
                .Include(c => c.Project)
                .FirstOrDefault(c => c.ProjectId == top.ProjectId && c.WorkPackageId == top.WorkPackageId)!;
        }
        if (top.ChildWorkPackages != null && top.ChildWorkPackages.Count() != 0)
        {
            foreach (var wp in top.ChildWorkPackages)
            {
                foreach (var item in FindAllChildren(wp))
                {
                    wps.Add(item);
                }
            }
        }
        return wps;
    }
}
