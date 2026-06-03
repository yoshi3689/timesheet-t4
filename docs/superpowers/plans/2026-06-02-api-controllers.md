# API Controllers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose every MVC controller action as a JSON REST endpoint under `api/[controller]`, and enable the React dev server at `http://localhost:3000` to call them with credentials.

**Architecture:** One `ControllerBase` class per domain lives in `Controllers/Api/`. Each class injects the same service interface(s) as its MVC counterpart. The only structural difference from the MVC controllers is that WorkPackagesController replaces `HttpContext.Session.GetInt32("CurrentProject")` with an explicit `{projectId}` route segment, because REST clients have no session.

**Tech Stack:** ASP.NET Core 8, `[ApiController]`, Identity (`UserManager<ApplicationUser>`), existing service interfaces (`IEmployeeService`, `IProjectService`, `IWorkPackageService`, `ITimesheetService`, `INotificationService`).

---

## File Map

| Action | Path |
|--------|------|
| Create | `Controllers/Api/EmployeesController.cs` |
| Create | `Controllers/Api/ProjectsController.cs` |
| Create | `Controllers/Api/WorkPackagesController.cs` |
| Create | `Controllers/Api/TimesheetsController.cs` |
| Create | `Controllers/Api/NotificationsController.cs` |
| Modify | `Program.cs` |

---

### Task 1: CORS configuration in Program.cs

**Files:**
- Modify: `Program.cs`

**Context:** `UseCors` must be placed after `UseRouting` and before `UseAuthentication`. Service registration goes before `builder.Build()`.

- [ ] **Step 1: Add the CORS service registration**

In `Program.cs`, after line 59 (`builder.Services.AddHealthChecks();`) and before `var app = builder.Build();`, insert:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

- [ ] **Step 2: Apply the CORS middleware**

In `Program.cs`, after `app.UseRouting();` (line 80) and before `app.UseAuthentication();`, insert:

```csharp
app.UseCors("Frontend");
```

- [ ] **Step 3: Build and verify no compile errors**

```bash
cd /Users/yoshi/dev/timesheet-t4 && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add Program.cs
git commit -m "feat: add CORS policy for http://localhost:3000 with credentials"
```

---

### Task 2: EmployeesController

Mirrors: `EmployeeManagerController` (paginated list, details, update, labour grades) and `EmployeeProjectController` (available-for-project, add-to-project, assign-tsa).

**Files:**
- Create: `Controllers/Api/EmployeesController.cs`

- [ ] **Step 1: Create the file**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "KeyRequirement")]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeesController(
            IEmployeeService employeeService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _employeeService = employeeService;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET /api/employees?page=1&pageSize=50
        [HttpGet]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50)
        {
            var users = await _employeeService.GetAllEmployeesPaginatedAsync(page, pageSize);
            int totalCount = _employeeService.GetTotalEmployeeCount();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return Ok(new { users, totalPages, currentPage = page, pageSize });
        }

        // GET /api/employees/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _employeeService.GetEmployeeDetailsAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        // PUT /api/employees/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ApplicationUser applicationUser)
        {
            if (id != applicationUser.Id) return BadRequest();
            var existing = await _employeeService.FindEmployeeAsync(id);
            if (existing == null) return NotFound();
            await _employeeService.UpdateEmployeeAsync(existing, applicationUser, _userManager);
            return NoContent();
        }

        // GET /api/employees/timesheet-approvers?supervisorId=xxx
        [HttpGet("timesheet-approvers")]
        public IActionResult GetTimesheetApprovers([FromQuery] string supervisorId)
        {
            return Ok(_employeeService.GetTimesheetApprovers(supervisorId));
        }

        // GET /api/employees/available-for-project?projectId=1
        [HttpGet("available-for-project")]
        public IActionResult GetAvailableForProject([FromQuery] int projectId)
        {
            var user = _employeeService.GetCurrentUserWithSupervisedUsers(User.Identity!.Name!);
            if (user == null) return BadRequest();
            var users = _employeeService.GetAvailableUsersForProject(
                projectId, user.Id, user.Id, User.IsInRole("Admin"));
            return Ok(users);
        }

        // POST /api/employees/add-to-project
        [HttpPost("add-to-project")]
        public IActionResult AddToProject([FromBody] List<EmployeeProject> employeeProjects)
        {
            _employeeService.AddEmployeesToProject(employeeProjects);
            return Ok(employeeProjects);
        }

        // POST /api/employees/assign-tsa
        [HttpPost("assign-tsa")]
        public async Task<IActionResult> AssignTSApprover([FromBody] EmployeeProject employeeProject)
        {
            var user = _employeeService.GetCurrentUserWithSupervisedUsers(User.Identity!.Name!);
            var futureTSA = await _employeeService.FindEmployeeAsync(employeeProject.UserId!);
            if (user == null || futureTSA == null ||
                (futureTSA.SupervisorId != user.Id && futureTSA.Id != user.Id))
                return BadRequest();
            _employeeService.AssignTimesheetApprover(user, employeeProject.UserId!);
            return Ok();
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /Users/yoshi/dev/timesheet-t4 && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Controllers/Api/EmployeesController.cs
git commit -m "feat: add api/employees controller"
```

---

### Task 3: ProjectsController

Mirrors: `ProjectController` — project list, create, employees, close, assign ASM, find PM, and the three PDF report endpoints.

**Files:**
- Create: `Controllers/Api/ProjectsController.cs`

- [ ] **Step 1: Create the file**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "KeyRequirement")]
    public class ProjectsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly IWorkPackageService _workPackageService;

        public ProjectsController(
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            IWorkPackageService workPackageService)
        {
            _userManager = userManager;
            _projectService = projectService;
            _workPackageService = workPackageService;
        }

        // GET /api/projects
        [HttpGet]
        public IActionResult GetAll()
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isHrOrAdmin = User.IsInRole("HR") || User.IsInRole("Admin");
            return Ok(_projectService.GetProjectsForUser(userId!, isHrOrAdmin));
        }

        // POST /api/projects
        [HttpPost]
        [Authorize(Roles = "HR,Admin")]
        public IActionResult Create([FromBody] CreateProjectViewModel input)
        {
            var (valid, error) = _projectService.ValidateNewProject(input);
            if (!valid) return BadRequest(new { error });
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _projectService.CreateProject(input);
            return Ok();
        }

        // GET /api/projects/{id}/employees
        [HttpGet("{id}/employees")]
        public async Task<IActionResult> GetAllEmployees(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var employees = await _projectService.GetAllProjectEmployeesAsync(id, user.Id);
            return Ok(employees);
        }

        // POST /api/projects/{id}/close
        [HttpPost("{id}/close")]
        public async Task<IActionResult> CloseProject(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Forbid();
            if (id == 10) return BadRequest();
            _workPackageService.CloseProject(id);
            return Ok();
        }

        // POST /api/projects/{id}/asm
        [HttpPost("{id}/asm")]
        public async Task<IActionResult> AssignASM(int id, [FromBody] string asm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            bool success = await _projectService.AssignAssistantProjectManagerAsync(id, asm, user.Id);
            if (!success) return BadRequest();
            return Ok();
        }

        // GET /api/projects/{id}/pm
        [HttpGet("{id}/pm")]
        public async Task<IActionResult> FindPM(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Forbid();
            return Ok(userId);
        }

        // GET /api/projects/{id}/report
        [HttpGet("{id}/report")]
        public async Task<IActionResult> Report(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Forbid();
            var bytes = await _projectService.GenerateReportAsync(id);
            if (bytes.Length == 0) return BadRequest();
            return File(bytes, "application/pdf", $"Report-{id}-{DateTime.Now:d}.pdf");
        }

        // GET /api/projects/{id}/week-report
        [HttpGet("{id}/week-report")]
        public async Task<IActionResult> WeekReport(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Forbid();
            var bytes = await _projectService.GenerateWeekReportAsync(id);
            if (bytes.Length == 0) return BadRequest();
            return File(bytes, "application/pdf", $"WeekReport-{id}-{DateTime.Now:d}.pdf");
        }

        // GET /api/projects/{id}/pcbac
        [HttpGet("{id}/pcbac")]
        public async Task<IActionResult> PCBAC(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Forbid();
            var bytes = await _projectService.GeneratePCBACAsync(id);
            if (bytes.Length == 0) return BadRequest();
            return File(bytes, "application/pdf", $"PCBAC-{id}-{DateTime.Now:d}.pdf");
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /Users/yoshi/dev/timesheet-t4 && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Controllers/Api/ProjectsController.cs
git commit -m "feat: add api/projects controller"
```

---

### Task 4: WorkPackagesController

Mirrors: `WorkPackageController` (responsible WPs, details, budgets+estimates, edit model) and the WP-specific actions from `ProjectController` (tree, split, budget-details, close-wp, wp-employees, candidate/assigned employees, assign employees, assign RE).

**Key design change from MVC:** The MVC `ProjectController` reads `HttpContext.Session.GetInt32("CurrentProject")` to know which project is active. The API version receives `{projectId}` as a route segment instead — callers must supply it explicitly.

**Files:**
- Create: `Controllers/Api/WorkPackagesController.cs`

- [ ] **Step 1: Create the file**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "KeyRequirement")]
    public class WorkPackagesController : ControllerBase
    {
        private readonly IWorkPackageService _workPackageService;
        private readonly IProjectService _projectService;
        private readonly UserManager<ApplicationUser> _userManager;

        public WorkPackagesController(
            IWorkPackageService workPackageService,
            IProjectService projectService,
            UserManager<ApplicationUser> userManager)
        {
            _workPackageService = workPackageService;
            _projectService = projectService;
            _userManager = userManager;
        }

        // GET /api/workpackages/responsible
        [HttpGet("responsible")]
        public async Task<IActionResult> GetResponsible()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(await _workPackageService.GetResponsibleWorkPackagesAsync(user.Id));
        }

        // GET /api/workpackages/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetails(string id)
        {
            var wp = await _workPackageService.GetWorkPackageDetailsAsync(id);
            if (wp == null) return NotFound();
            return Ok(wp);
        }

        // POST /api/workpackages/budgets-and-estimates
        [HttpPost("budgets-and-estimates")]
        public IActionResult CreateBudgetsAndEstimates([FromBody] LowestWorkPackageBAndEViewModel input)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _workPackageService.CreateBudgetsAndEstimates(input);
            return Ok();
        }

        // GET /api/workpackages/project/{projectId}/tree
        [HttpGet("project/{projectId}/tree")]
        public async Task<IActionResult> GetProjectTree(int projectId)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            var wps = _workPackageService.GetProjectWorkPackagesTree(projectId);
            var budgets = _workPackageService.GetProjectBudgets(projectId);
            wps = _workPackageService.CalculateTotalMoney(wps, budgets);
            return Ok(wps);
        }

        // POST /api/workpackages/project/{projectId}/split
        [HttpPost("project/{projectId}/split")]
        public async Task<IActionResult> Split(int projectId, [FromBody] WorkPackageViewModel p)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();

            var (valid, error) = _workPackageService.ValidateNewWorkPackage(p, projectId);
            if (!valid) return BadRequest(new { error });

            var wps = _workPackageService.GetProjectWorkPackagesTree(projectId);
            var parent = wps.FirstOrDefault(c => c.WorkPackageId == p.WorkPackage!.ParentWorkPackageId);
            if (parent != null && parent.IsClosed) return BadRequest();

            return Ok(_workPackageService.CreateChildWorkPackage(p, projectId));
        }

        // POST /api/workpackages/project/{projectId}/budget-details
        [HttpPost("project/{projectId}/budget-details")]
        public async Task<IActionResult> BudgetDetails(int projectId, [FromBody] WorkPackage wp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            var budgets = _workPackageService.GetBudgetDetails(wp.WorkPackageId, projectId);
            return Ok(new
            {
                pmBudgets = budgets.Where(c => !c.isREBudget).ToList(),
                reBudgets = budgets.Where(c => c.isREBudget).ToList()
            });
        }

        // POST /api/workpackages/project/{projectId}/close-wp
        [HttpPost("project/{projectId}/close-wp")]
        public async Task<IActionResult> CloseWorkPackage(int projectId, [FromBody] WorkPackage wp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            _workPackageService.CloseWorkPackage(wp.WorkPackageId, projectId);
            return Ok();
        }

        // POST /api/workpackages/project/{projectId}/wp-employees
        [HttpPost("project/{projectId}/wp-employees")]
        public async Task<IActionResult> GetWPEmployees(int projectId, [FromBody] WorkPackage wp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            return Ok(_workPackageService.GetWPEmployees(wp.WorkPackageId, projectId));
        }

        // POST /api/workpackages/project/{projectId}/candidate-employees
        [HttpPost("project/{projectId}/candidate-employees")]
        public async Task<IActionResult> GetCandidateEmployees(int projectId, [FromBody] WorkPackage wp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            var result = _workPackageService.GetCandidateEmployees(wp.WorkPackageId, projectId);
            if (result == null) return BadRequest();
            return Ok(result);
        }

        // POST /api/workpackages/project/{projectId}/assigned-employees
        [HttpPost("project/{projectId}/assigned-employees")]
        public async Task<IActionResult> GetAssignedEmployees(int projectId, [FromBody] WorkPackage wp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            return Ok(_workPackageService.GetAssignedEmployees(wp.WorkPackageId, projectId));
        }

        // POST /api/workpackages/project/{projectId}/assign-employees
        [HttpPost("project/{projectId}/assign-employees")]
        public async Task<IActionResult> AssignEmployees(int projectId, [FromBody] List<EmployeeWorkPackage> ewps)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            if (ewps.Count == 0) return BadRequest();
            for (int i = 1; i < ewps.Count; i++)
            {
                if (ewps[i].WorkPackageId != ewps[0].WorkPackageId ||
                    ewps[i].WorkPackageProjectId != ewps[0].WorkPackageProjectId)
                    return BadRequest();
            }
            var result = _workPackageService.AssignEmployees(ewps);
            if (result == null) return BadRequest();
            return Ok(result);
        }

        // POST /api/workpackages/project/{projectId}/assign-re
        [HttpPost("project/{projectId}/assign-re")]
        public async Task<IActionResult> AssignResponsibleEngineer(int projectId, [FromBody] EmployeeWorkPackage ewp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Forbid();
            var result = _workPackageService.AssignResponsibleEngineer(ewp);
            if (result == null) return BadRequest();
            return Ok(result);
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /Users/yoshi/dev/timesheet-t4 && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Controllers/Api/WorkPackagesController.cs
git commit -m "feat: add api/workpackages controller"
```

---

### Task 5: TimesheetsController

Mirrors: `TimesheetController` — unapproved/approved/to-approve lists, create, get by ID, update row, submit/approve/decline, add custom row.

**Files:**
- Create: `Controllers/Api/TimesheetsController.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "KeyRequirement")]
    public class TimesheetsController : ControllerBase
    {
        private readonly ITimesheetService _timesheetService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TimesheetsController(
            ITimesheetService timesheetService,
            UserManager<ApplicationUser> userManager)
        {
            _timesheetService = timesheetService;
            _userManager = userManager;
        }

        // GET /api/timesheets/unapproved
        [HttpGet("unapproved")]
        public IActionResult GetUnapproved()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(_timesheetService.GetUnapprovedTimesheets(userId!));
        }

        // GET /api/timesheets/approved
        [HttpGet("approved")]
        public IActionResult GetApproved()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(_timesheetService.GetApprovedTimesheets(userId!).Select(t => new Timesheet
            {
                TotalHours = t.TotalHours,
                EndDate = t.EndDate,
                TimesheetId = t.TimesheetId,
                EmployeeHash = t.EmployeeHash,
                FlexHours = t.FlexHours,
                Overtime = t.Overtime
            }));
        }

        // GET /api/timesheets/to-approve
        [HttpGet("to-approve")]
        public IActionResult GetToApprove()
        {
            var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(_timesheetService.GetTimesheetsToApprove(approverId!));
        }

        // POST /api/timesheets
        [HttpPost]
        public IActionResult CreateTimesheet([FromBody] string end)
        {
            if (string.IsNullOrWhiteSpace(end))
                return BadRequest("Please choose a date.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int offset = (7 - (int)Convert.ToDateTime(end).DayOfWeek + (int)DayOfWeek.Friday) % 7;
            DateTime nextFriday = Convert.ToDateTime(end).AddDays(offset);

            var existingSheets = _timesheetService.GetUnapprovedTimesheets(userId!);
            if (existingSheets.Any(s => s.EndDate == DateOnly.FromDateTime(nextFriday)))
                return BadRequest("Timesheet already exists for this week.");

            var created = _timesheetService.CreateOrUpdateTimesheetWithRows(Convert.ToDateTime(end), userId!);
            if (created == null)
                return BadRequest("Timesheet already exists for this week.");

            return Ok(new Timesheet
            {
                TotalHours = created.TotalHours,
                EndDate = created.EndDate,
                TimesheetId = created.TimesheetId,
                EmployeeHash = created.EmployeeHash,
                FlexHours = created.FlexHours,
                Overtime = created.Overtime
            });
        }

        // POST /api/timesheets/rows/update
        [HttpPost("rows/update")]
        public IActionResult UpdateRow([FromBody] TimesheetRow timesheetRow)
        {
            if (timesheetRow.ValidationErrors != null)
                return BadRequest(timesheetRow.ValidationErrors);

            var (errors, result) = _timesheetService.UpdateRow(timesheetRow);
            if (result == null && errors == null) return BadRequest();
            if (errors != null) return BadRequest(errors);
            return Ok(result);
        }

        // POST /api/timesheets/get
        [HttpPost("get")]
        public IActionResult GetTimesheet([FromBody] string timesheetId)
        {
            int tid;
            try { tid = Convert.ToInt32(timesheetId); }
            catch { return BadRequest(); }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var timesheet = _timesheetService.GetTimesheetWithDetails(tid);
            if (timesheet == null ||
                (timesheet.UserId != userId && timesheet.User!.TimesheetApproverId != userId))
                return BadRequest();

            _timesheetService.CreateOrUpdateTimesheetWithRows(
                DateTime.Parse(timesheet.EndDate.ToString()!), timesheet.UserId ?? "0");
            return Ok(_timesheetService.GetTimesheetRowDtos(tid));
        }

        // POST /api/timesheets/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitTimesheet([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null) return BadRequest();

            var (success, error, _) = await _timesheetService.SubmitTimesheetAsync(
                model.Timesheet, user.Id, model.Password, model.Flexhours, model.Overtime);

            if (!success)
            {
                if (error == "badrequest") return BadRequest();
                if (error == "unauthorized") return Unauthorized();
                return BadRequest(error);
            }
            return Ok(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        // POST /api/timesheets/approve
        [HttpPost("approve")]
        public async Task<IActionResult> ApproveTimesheet([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null) return BadRequest();

            var (success, error, _) = await _timesheetService.ApproveTimesheetAsync(
                model.Timesheet, user.Id, model.Password);

            if (!success)
            {
                if (error == "badrequest") return BadRequest();
                if (error == "unauthorized") return Unauthorized();
                return BadRequest(error);
            }
            return Ok(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        // POST /api/timesheets/decline
        [HttpPost("decline")]
        public async Task<IActionResult> DeclineTimesheet([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null) return BadRequest();

            bool success = await _timesheetService.DeclineTimesheetAsync(
                model.Timesheet, user.Id, model.Password, model.ApproverNotes);
            if (!success) return BadRequest();
            return Ok(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        // POST /api/timesheets/rows/custom
        [HttpPost("rows/custom")]
        public async Task<IActionResult> AddCustomRow([FromBody] CustomRowModel model)
        {
            int timesheetIdInt;
            try { timesheetIdInt = Convert.ToInt32(model.TimesheetId); }
            catch { return BadRequest(); }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return BadRequest();

            var row = _timesheetService.AddCustomRow(
                timesheetIdInt, user.Id, model.Type!, user.LabourGradeCode);
            if (row == null) return BadRequest();
            return Ok(row);
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /Users/yoshi/dev/timesheet-t4 && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Controllers/Api/TimesheetsController.cs
git commit -m "feat: add api/timesheets controller"
```

---

### Task 6: NotificationsController

Mirrors: notification actions from `HomeController` — list and dismiss.

**Files:**
- Create: `Controllers/Api/NotificationsController.cs`

- [ ] **Step 1: Create the file**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "KeyRequirement")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        // GET /api/notifications
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(_notificationService.GetUserNotifications(user.Id));
        }

        // POST /api/notifications/dismiss
        [HttpPost("dismiss")]
        public async Task<IActionResult> Dismiss([FromBody] string id)
        {
            int newId;
            try { newId = Convert.ToInt32(id); }
            catch { return BadRequest(); }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _notificationService.DismissNotificationAsync(newId, user.Id);
            return Ok();
        }
    }
}
```

- [ ] **Step 2: Build and verify final state**

```bash
cd /Users/yoshi/dev/timesheet-t4 && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Controllers/Api/NotificationsController.cs
git commit -m "feat: add api/notifications controller"
```

---

## Endpoint Reference

| Method | URL | Controller action |
|--------|-----|-------------------|
| GET | `/api/employees` | `EmployeesController.GetAll` |
| GET | `/api/employees/{id}` | `EmployeesController.GetById` |
| PUT | `/api/employees/{id}` | `EmployeesController.Update` |
| GET | `/api/employees/timesheet-approvers?supervisorId=` | `EmployeesController.GetTimesheetApprovers` |
| GET | `/api/employees/available-for-project?projectId=` | `EmployeesController.GetAvailableForProject` |
| POST | `/api/employees/add-to-project` | `EmployeesController.AddToProject` |
| POST | `/api/employees/assign-tsa` | `EmployeesController.AssignTSApprover` |
| GET | `/api/projects` | `ProjectsController.GetAll` |
| POST | `/api/projects` | `ProjectsController.Create` |
| GET | `/api/projects/{id}/employees` | `ProjectsController.GetAllEmployees` |
| POST | `/api/projects/{id}/close` | `ProjectsController.CloseProject` |
| POST | `/api/projects/{id}/asm` | `ProjectsController.AssignASM` |
| GET | `/api/projects/{id}/pm` | `ProjectsController.FindPM` |
| GET | `/api/projects/{id}/report` | `ProjectsController.Report` |
| GET | `/api/projects/{id}/week-report` | `ProjectsController.WeekReport` |
| GET | `/api/projects/{id}/pcbac` | `ProjectsController.PCBAC` |
| GET | `/api/workpackages/responsible` | `WorkPackagesController.GetResponsible` |
| GET | `/api/workpackages/{id}` | `WorkPackagesController.GetDetails` |
| POST | `/api/workpackages/budgets-and-estimates` | `WorkPackagesController.CreateBudgetsAndEstimates` |
| GET | `/api/workpackages/project/{projectId}/tree` | `WorkPackagesController.GetProjectTree` |
| POST | `/api/workpackages/project/{projectId}/split` | `WorkPackagesController.Split` |
| POST | `/api/workpackages/project/{projectId}/budget-details` | `WorkPackagesController.BudgetDetails` |
| POST | `/api/workpackages/project/{projectId}/close-wp` | `WorkPackagesController.CloseWorkPackage` |
| POST | `/api/workpackages/project/{projectId}/wp-employees` | `WorkPackagesController.GetWPEmployees` |
| POST | `/api/workpackages/project/{projectId}/candidate-employees` | `WorkPackagesController.GetCandidateEmployees` |
| POST | `/api/workpackages/project/{projectId}/assigned-employees` | `WorkPackagesController.GetAssignedEmployees` |
| POST | `/api/workpackages/project/{projectId}/assign-employees` | `WorkPackagesController.AssignEmployees` |
| POST | `/api/workpackages/project/{projectId}/assign-re` | `WorkPackagesController.AssignResponsibleEngineer` |
| GET | `/api/timesheets/unapproved` | `TimesheetsController.GetUnapproved` |
| GET | `/api/timesheets/approved` | `TimesheetsController.GetApproved` |
| GET | `/api/timesheets/to-approve` | `TimesheetsController.GetToApprove` |
| POST | `/api/timesheets` | `TimesheetsController.CreateTimesheet` |
| POST | `/api/timesheets/rows/update` | `TimesheetsController.UpdateRow` |
| POST | `/api/timesheets/get` | `TimesheetsController.GetTimesheet` |
| POST | `/api/timesheets/submit` | `TimesheetsController.SubmitTimesheet` |
| POST | `/api/timesheets/approve` | `TimesheetsController.ApproveTimesheet` |
| POST | `/api/timesheets/decline` | `TimesheetsController.DeclineTimesheet` |
| POST | `/api/timesheets/rows/custom` | `TimesheetsController.AddCustomRow` |
| GET | `/api/notifications` | `NotificationsController.GetAll` |
| POST | `/api/notifications/dismiss` | `NotificationsController.Dismiss` |
