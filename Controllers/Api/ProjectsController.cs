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
        private const int ExtrasProjectId = 10;
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
            var projects = _projectService.GetProjectsForUser(userId!, isHrOrAdmin);
            return Ok(projects.Select(p => new {
                p.ProjectId,
                p.ProjectTitle,
                p.ProjectManagerId,
                p.AssistantProjectManagerId,
                p.TotalBudget,
                p.ActualCost,
                p.IsClosed,
                ProjectManager = p.ProjectManager == null ? null : (object)new {
                    p.ProjectManager.FirstName,
                    p.ProjectManager.LastName
                }
            }));
        }

        // GET /api/projects/{id}
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isHrOrAdmin = User.IsInRole("HR") || User.IsInRole("Admin");
            var project = _projectService.GetProjectByIdForUser(id, userId!, isHrOrAdmin);
            if (project == null) return NotFound();
            return Ok(new {
                project.ProjectId,
                project.ProjectTitle,
                project.ProjectManagerId,
                project.AssistantProjectManagerId,
                project.TotalBudget,
                project.ActualCost,
                project.IsClosed,
                ProjectManager = project.ProjectManager == null ? null : (object)new {
                    project.ProjectManager.FirstName,
                    project.ProjectManager.LastName
                }
            });
        }

        // POST /api/projects
        [HttpPost]
        [Authorize(Roles = "HR,Admin")]
        public IActionResult Create([FromBody] CreateProjectViewModel input)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var (valid, error) = _projectService.ValidateNewProject(input);
            if (!valid) return BadRequest(new { error });
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
            if (id == ExtrasProjectId) return BadRequest();
            _workPackageService.CloseProject(id);
            return Ok();
        }

        // POST /api/projects/{id}/asm
        [HttpPost("{id}/asm")]
        public async Task<IActionResult> AssignASM(int id, [FromBody] string asm)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            bool isPM = await _projectService.VerifyProjectManagerAsync(id, userId!);
            if (!isPM) return Forbid();
            bool success = await _projectService.AssignAssistantProjectManagerAsync(id, asm, userId!);
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
