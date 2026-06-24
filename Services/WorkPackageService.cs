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

    public async Task<List<WorkPackage>> GetAssignedWorkPackagesAsync(string userId)
    {
        return await _context.EmployeeWorkPackages!
            .Where(ewp => ewp.UserId == userId)
            .Select(ewp => ewp.WorkPackage!)
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
        for (int i = 0; i < budgets.Count; i++)
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
            item.Rate = lgs.Where(c => c.LabourCode == item.LabourCode).FirstOrDefault()?.Rate ?? 0;
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
                parentB = parentBudgets.Where(c => c.LabourCode == budget.LabourCode).FirstOrDefault();
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
        // Load all WPs at once — EF relationship fixup wires up ChildWorkPackages
        // for the full tree, so FindAllChildren traverses in memory with no N+1.
        var all = _context.WorkPackages!
            .Where(c => c.ProjectId == projectId)
            .Include(c => c.ResponsibleUser)
            .Include(c => c.ParentWorkPackage)
            .Include(c => c.ChildWorkPackages)
            .ToList();

        var assigneeCounts = _context.EmployeeWorkPackages!
            .Where(ewp => ewp.WorkPackageProjectId == projectId)
            .GroupBy(ewp => ewp.WorkPackageId)
            .Select(g => new { WorkPackageId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.WorkPackageId, x => x.Count);

        foreach (var wp in all)
            wp.AssigneeCount = assigneeCounts.TryGetValue(wp.WorkPackageId, out var c) ? c : 0;

        var roots = all.Where(c => c.ParentWorkPackage == null).ToList();
        if (roots.Count == 0) return [];
        var result = new List<WorkPackage>();
        foreach (var root in roots)
            result.AddRange(FindAllChildren(root));
        return result;
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
                var budget = budgets.FirstOrDefault(c => c.WPProjectId == (wp.ProjectId + "~" + wp.WorkPackageId) && c.LabourCode == lg.LabourCode);
                if (budget == null) continue;
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
        return (true, null);
    }

    public WorkPackage CreateChildWorkPackage(WorkPackageViewModel p, int projectId)
    {
        string newWPID = Guid.NewGuid().ToString();
        string? requestedParentId = p.WorkPackage!.ParentWorkPackageId;
        if (string.IsNullOrEmpty(requestedParentId) || requestedParentId == "0")
        {
            // Resolve "no parent" / "0" sentinel to the explicit root WP (id="0") if one exists.
            // For seeded projects that have no "0" root, leave requestedParentId null so the new
            // WP is created as a top-level sibling of the existing phase WPs (A/B/C/D).
            var rootWP = _context.WorkPackages!
                .FirstOrDefault(c => c.ProjectId == projectId && c.WorkPackageId == "0");
            requestedParentId = rootWP?.WorkPackageId;
        }

        if (requestedParentId != null)
        {
            var parent = _context.WorkPackages!.FirstOrDefault(c => c.ProjectId == projectId && c.WorkPackageId == requestedParentId);
            if (parent != null)
            {
                parent.IsBottomLevel = false;
                _context.Entry(parent).State = EntityState.Modified;
                var staleAssignments = _context.EmployeeWorkPackages!
                    .Where(e => e.WorkPackageId == parent.WorkPackageId && e.WorkPackageProjectId == projectId);
                _context.EmployeeWorkPackages!.RemoveRange(staleAssignments);
            }
        }

        var newChild = new WorkPackage
        {
            WorkPackageId = newWPID,
            ProjectId = projectId,
            ParentWorkPackageId = requestedParentId,
            IsBottomLevel = true,
            IsClosed = false,
            Title = p.WorkPackage.Title
        };

        if (p.budgets != null)
        {
            List<Budget> parentBudgets = _context.Budgets.Where(c => c.WPProjectId == projectId + "~" + newChild.ParentWorkPackageId).ToList();

            foreach (var budget in p.budgets)
            {
                var parentB = parentBudgets.FirstOrDefault(c => c.LabourCode == budget.LabourCode);
                if (parentB != null && budget.Days * budget.People > parentB.UnallocatedDays * parentB.UnallocatedPeople)
                    throw new InvalidOperationException($"Budget for labour grade '{budget.LabourCode}' exceeds the parent work package's unallocated budget.");
            }

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
                var parentB = parentBudgets.FirstOrDefault(c => c.LabourCode == budget.LabourCode);
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
                FirstName = "",
                LastName = ""
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
            budget.Rate = lgs.Where(c => c.LabourCode == budget.LabourCode).FirstOrDefault()?.Rate ?? 0;
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

    public void UpdateWorkPackage(string workPackageId, int projectId, string title, string? responsibleUserId)
    {
        var wp = _context.WorkPackages
            .SingleOrDefault(w => w.WorkPackageId == workPackageId && w.ProjectId == projectId);
        if (wp == null) return;
        wp.Title = title;
        wp.ResponsibleUserId = responsibleUserId;
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

        var emp = _context.Users
            .Where(u => u.LabourGradeCode != null)
            .Select(u => new EmployeeWorkPackageViewModel
            {
                Employee = u,
                Assigned = userIdsInLLWP.Contains(u.Id)
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
        if (!ewps.Any()) return "Error";
        var workPackageId = ewps[0].WorkPackageId;
        var workPackageProjectId = ewps[0].WorkPackageProjectId;

        var oldWp = _context.WorkPackages
            .Where(c => ewps.Select(s => s.WorkPackageId).Contains(c.WorkPackageId) && c.ProjectId == workPackageProjectId && c.IsBottomLevel == false)
            .ToList();
        if (oldWp.Any()) return "Error";

        var currentWp = _context.WorkPackages
            .Where(c => c.WorkPackageId == workPackageId && c.ProjectId == workPackageProjectId)
            .Include(c => c.Project)
            .FirstOrDefault();
        if (currentWp == null) return "Error";
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
            .Where(c => c.UserId == ewp.UserId && c.WorkPackageId == ewp.WorkPackageId && c.WorkPackageProjectId == ewp.WorkPackageProjectId)
            .Include(c => c.WorkPackage)
            .Include(c => c.WorkPackage!.Project)
            .FirstOrDefault();
        if (fullEwp == null) return null;

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
            var matchingBudget = projectBudget.FirstOrDefault(c => c.LabourCode == item.LabourCode);
            if (matchingBudget == null) continue;
            emptyBudgets.Add(new Budget
            {
                LabourCode = item.LabourCode,
                isREBudget = false,
                Rate = item.Rate,
                UnallocatedDays = matchingBudget.UnallocatedDays,
                UnallocatedPeople = matchingBudget.UnallocatedPeople
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
        var wps = new List<WorkPackage> { top };
        foreach (var child in top.ChildWorkPackages)
            wps.AddRange(FindAllChildren(child));
        return wps;
    }
}
