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
