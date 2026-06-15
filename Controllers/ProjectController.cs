using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers
{
    [Authorize(Policy = "KeyRequirement")]
    public class ProjectController : Controller
    {
        private readonly ILogger<ProjectController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly IWorkPackageService _workPackageService;

        public ProjectController(
            ILogger<ProjectController> logger,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            IWorkPackageService workPackageService)
        {
            _logger = logger;
            _userManager = userManager;
            _projectService = projectService;
            _workPackageService = workPackageService;
        }

        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Index()
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isHrOrAdmin = User.Identity!.IsAuthenticated && (User.IsInRole("HR") || User.IsInRole("Admin"));
            var projects = _projectService.GetProjectsForUser(userId!, isHrOrAdmin);
            return View(projects);
        }

        [Authorize(Policy = "KeyRequirement", Roles = "HR,Admin")]
        public IActionResult Create()
        {
            var users = _userManager.Users.ToList();
            ViewData["users"] = users;
            var lgs = _workPackageService.GetCurrentYearLabourGrades();
            CreateProjectViewModel proj = new CreateProjectViewModel
            {
                budgets = lgs.Select(lg => new Budget
                {
                    LabourCode = lg.LabourCode,
                    isREBudget = false,
                    Rate = lg.Rate,
                }).ToList()
            };
            return View(proj);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == 10)
            {
                return RedirectToAction("Index");
            }
            HttpContext.Session.SetInt32("CurrentProject", id ?? 0);

            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id ?? 0, userId!);
            if (!isPM) return Challenge();

            var wps = _workPackageService.GetProjectWorkPackagesTree(id ?? 0);
            var budgets = _workPackageService.GetProjectBudgets(id ?? 0);
            wps = _workPackageService.CalculateTotalMoney(wps, budgets);

            WorkPackageViewModel model = new WorkPackageViewModel
            {
                wps = wps,
                LabourGrades = _workPackageService.GetCurrentYearLabourGrades()
            };
            return View(model);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> AssignEmployeesAsync([FromBody] List<EmployeeWorkPackage> ewps)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(HttpContext.Session.GetInt32("CurrentProject") ?? 0, userId!);
            if (!isPM) return Challenge();

            if (ewps.Count == 0) return BadRequest();

            for (int i = 1; i < ewps.Count; i++)
            {
                if (ewps[i].WorkPackageId != ewps[0].WorkPackageId || ewps[i].WorkPackageProjectId != ewps[0].WorkPackageProjectId)
                    return BadRequest();
            }

            var result = _workPackageService.AssignEmployees(ewps);
            if (result == null) return BadRequest();
            return Json(result);
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> AssignResponsibleEngineerAsync([FromBody] EmployeeWorkPackage ewp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(HttpContext.Session.GetInt32("CurrentProject") ?? 0, userId!);
            if (!isPM) return Challenge();

            if (ewp == null) return new JsonResult("Error!");

            var result = _workPackageService.AssignResponsibleEngineer(ewp);
            if (result == null) return new JsonResult("Error!");
            return new JsonResult(result);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> ShowSplitAsync(string workPackageId)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Challenge();

            var emptyBudgets = _workPackageService.GetSplitBudgets(workPackageId, projectId);
            if (emptyBudgets == null || emptyBudgets.Count == 0) return BadRequest();

            var model = new WorkPackageViewModel { budgets = emptyBudgets };
            return PartialView("_CreateWorkPackagePartial", model);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> SplitAsync(WorkPackageViewModel p)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Challenge();

            if (ModelState.GetFieldValidationState("WorkPackage.ParentWorkPackageId") != ModelValidationState.Valid
                || ModelState.GetFieldValidationState("WorkPackage.WorkPackageId") != ModelValidationState.Valid
                || ModelState.GetFieldValidationState("WorkPackage.Title") != ModelValidationState.Valid
                || ModelState.GetFieldValidationState("budgets") != ModelValidationState.Valid)
            {
                Response.StatusCode = 400;
                return PartialView("_CreateWorkPackagePartial", p);
            }

            // check parent is not closed
            var wps = _workPackageService.GetProjectWorkPackagesTree(projectId);
            var parent = wps.FirstOrDefault(c => c.WorkPackageId == p.WorkPackage!.ParentWorkPackageId);
            if (parent != null && parent.IsClosed) return BadRequest();

            var newChild = _workPackageService.CreateChildWorkPackage(p, projectId);
            return Json(newChild);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> BudgetDetailsAsync([FromBody] WorkPackage wp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Challenge();

            var budgets = _workPackageService.GetBudgetDetails(wp.WorkPackageId, projectId);
            List<List<Budget>> result = new List<List<Budget>>();
            result.Add(budgets.Where(c => c.isREBudget == false).ToList());
            result.Add(budgets.Where(c => c.isREBudget == true).ToList());
            return Json(result);
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> CloseWPAsync([FromBody] WorkPackage wp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Challenge();

            if (wp == null) return BadRequest();
            _workPackageService.CloseWorkPackage(wp.WorkPackageId, projectId);
            return Ok();
        }

        [HttpPost]
        [Authorize(Roles = "HR,Admin", Policy = "KeyRequirement")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProjectViewModel input)
        {
            var (valid, error) = _projectService.ValidateNewProject(input);
            if (!valid)
            {
                ModelState.AddModelError("project.ProjectId", error!);
                Response.StatusCode = 400;
                ViewData["users"] = _userManager.Users.ToList();
                return View(input);
            }

            if (ModelState.IsValid)
            {
                await _projectService.CreateProjectAsync(input);
                return RedirectToAction("Index");
            }

            ViewData["users"] = _userManager.Users.ToList();
            return View(input);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> GetWPEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Challenge();

            return new JsonResult(_workPackageService.GetWPEmployees(LowestLevelWp.WorkPackageId, projectId));
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> GetCandidateEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Challenge();

            var result = _workPackageService.GetCandidateEmployees(LowestLevelWp.WorkPackageId, projectId);
            if (result == null) return BadRequest();
            return new JsonResult(result);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> GetAssignedEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
            if (!isPM) return Challenge();

            return new JsonResult(_workPackageService.GetAssignedEmployees(LowestLevelWp.WorkPackageId, projectId));
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> CloseProject([FromBody] int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Challenge();

            if (id == 10) return BadRequest();
            _workPackageService.CloseProject(id);
            return Ok();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error");
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Report(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Challenge();

            var bytes = await _projectService.GenerateReportAsync(id);
            if (bytes.Length == 0) return BadRequest();
            return File(bytes, "application/pdf", "Report-" + id + "-" + DateTime.Now.ToShortDateString() + ".pdf");
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> WeekReport(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Challenge();

            var bytes = await _projectService.GenerateWeekReportAsync(id);
            if (bytes.Length == 0) return BadRequest();
            return File(bytes, "application/pdf", "Report-" + id + "-" + DateTime.Now.ToShortDateString() + ".pdf");
        }

        [Authorize(Policy = "KeyRequirement")]
        [HttpGet]
        public async Task<IActionResult> GetAllEmployees()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            var employees = await _projectService.GetAllProjectEmployeesAsync(projectId, user.Id);
            return Json(employees);
        }

        [Authorize(Policy = "KeyRequirement")]
        [HttpPost]
        public async Task<IActionResult> AssignASM([FromBody] String? asm)
        {
            if (asm == null) return BadRequest();
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            bool success = await _projectService.AssignAssistantProjectManagerAsync(projectId, asm, user.Id);
            if (!success) return new JsonResult("Error!");
            return Ok();
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> PCBAC(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Challenge();

            var bytes = await _projectService.GeneratePCBACAsync(id);
            if (bytes.Length == 0) return BadRequest();
            return File(bytes, "application/pdf", "Report-" + id + "-" + DateTime.Now.ToShortDateString() + ".pdf");
        }

        [Authorize(Policy = "KeyRequirement")]
        [HttpGet]
        public async Task<IActionResult> FindPM()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            return Json(user.Id);
        }
    }
}
