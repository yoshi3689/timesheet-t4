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

    // GET /api/workpackages/responsible/budget
    [HttpGet("responsible/budget")]
    public async Task<IActionResult> GetResponsibleBudget()
    {
      var user = await _userManager.GetUserAsync(User);
      if (user == null) return Unauthorized();
      return Ok(await _workPackageService.GetResponsibleSubtreeWithBudgetAsync(user.Id));
    }

    // GET /api/workpackages/assigned
    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssigned()
    {
      var user = await _userManager.GetUserAsync(User);
      if (user == null) return Unauthorized();
      return Ok(await _workPackageService.GetAssignedWorkPackagesAsync(user.Id));
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
    public async Task<IActionResult> CreateBudgetsAndEstimates([FromBody] LowestWorkPackageBAndEViewModel input)
    {
      if (!ModelState.IsValid) return BadRequest(ModelState);

      // Extract projectId from WPProjectId (format: "{projectId}~{wpId}")
      string? wpProjectId = input.budgets?.FirstOrDefault()?.WPProjectId
          ?? input.estimates?.FirstOrDefault()?.WPProjectId;
      if (wpProjectId == null) return BadRequest();
      if (!int.TryParse(wpProjectId.Split('~')[0], out int projectId)) return BadRequest();

      string userId = _userManager.GetUserId(HttpContext.User)!;
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();

      _workPackageService.CreateBudgetsAndEstimates(input);
      return Ok();
    }

    // GET /api/workpackages/project/{projectId}/tree
    [HttpGet("project/{projectId}/tree")]
    public async Task<IActionResult> GetProjectTree(int projectId)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");
      var wps = _workPackageService.GetProjectWorkPackagesTree(projectId);
      if (isPM || isAdminOrHR)
      {
        var budgets = _workPackageService.GetProjectBudgets(projectId);
        wps = _workPackageService.CalculateTotalMoney(wps, budgets);
      }
      return Ok(wps);
    }

    // POST /api/workpackages/project/{projectId}/split
    [HttpPost("project/{projectId}/split")]
    public async Task<IActionResult> Split(int projectId, [FromBody] WorkPackageViewModel p)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      if (p.WorkPackage == null) return BadRequest();

      var project = _projectService.GetProjectById(projectId);
      if (project == null) return NotFound();
      if (project.IsClosed) return BadRequest();

      var wps = _workPackageService.GetProjectWorkPackagesTree(projectId);
      var parent = wps.FirstOrDefault(c => c.WorkPackageId == p.WorkPackage.ParentWorkPackageId);
      if (parent != null && parent.IsClosed) return BadRequest();

      try
      {
        return Ok(_workPackageService.CreateChildWorkPackage(p, projectId));
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new { message = ex.Message });
      }
    }

    // POST /api/workpackages/project/{projectId}/budget-details
    [HttpPost("project/{projectId}/budget-details")]
    public async Task<IActionResult> BudgetDetails(int projectId, [FromBody] WorkPackageIdRequest req)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      var budgets = _workPackageService.GetBudgetDetails(req.WorkPackageId, projectId);
      return Ok(new
      {
        pmBudgets = budgets.Where(c => !c.isREBudget).ToList(),
        reBudgets = budgets.Where(c => c.isREBudget).ToList()
      });
    }

    // POST /api/workpackages/project/{projectId}/close-wp
    [HttpPost("project/{projectId}/close-wp")]
    public async Task<IActionResult> CloseWorkPackage(int projectId, [FromBody] WorkPackageIdRequest req)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      _workPackageService.CloseWorkPackage(req.WorkPackageId, projectId);
      return Ok();
    }

    // POST /api/workpackages/project/{projectId}/update-wp
    [HttpPost("project/{projectId}/update-wp")]
    public async Task<IActionResult> UpdateWorkPackage(int projectId, [FromBody] WorkPackageUpdateRequest req)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
      _workPackageService.UpdateWorkPackage(req.WorkPackageId, projectId, req.Title, req.ResponsibleUserId);
      return Ok();
    }

    // POST /api/workpackages/project/{projectId}/wp-employees
    [HttpPost("project/{projectId}/wp-employees")]
    public async Task<IActionResult> GetWPEmployees(int projectId, [FromBody] WorkPackageIdRequest req)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      return Ok(_workPackageService.GetWPEmployees(req.WorkPackageId, projectId));
    }

    // POST /api/workpackages/project/{projectId}/candidate-employees
    [HttpPost("project/{projectId}/candidate-employees")]
    public async Task<IActionResult> GetCandidateEmployees(int projectId, [FromBody] WorkPackageIdRequest req)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      var result = _workPackageService.GetCandidateEmployees(req.WorkPackageId, projectId);
      if (result == null) return BadRequest();
      return Ok(result);
    }

    // POST /api/workpackages/project/{projectId}/assigned-employees
    [HttpPost("project/{projectId}/assigned-employees")]
    public async Task<IActionResult> GetAssignedEmployees(int projectId, [FromBody] WorkPackageIdRequest req)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin && !_workPackageService.IsEmployeeAssignedToWP(userId!, req.WorkPackageId, projectId))
        return Forbid();
      return Ok(_workPackageService.GetAssignedEmployees(req.WorkPackageId, projectId));
    }

    // POST /api/workpackages/project/{projectId}/assign-employees
    [HttpPost("project/{projectId}/assign-employees")]
    public async Task<IActionResult> AssignEmployees(int projectId, [FromBody] List<EmployeeWorkPackage> ewps)
    {
      var userId = _userManager.GetUserId(HttpContext.User);
      bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, userId!);
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      if (ewps.Count == 0) return BadRequest();
      var project = _projectService.GetProjectById(projectId);
      if (project == null) return NotFound();
      if (project.IsClosed) return BadRequest();
      for (int i = 0; i < ewps.Count; i++)
      {
        // Reject body items that reference a different project than the route
        if (ewps[i].WorkPackageProjectId != projectId)
          return BadRequest();
        if (i > 0 && (ewps[i].WorkPackageId != ewps[0].WorkPackageId ||
            ewps[i].WorkPackageProjectId != ewps[0].WorkPackageProjectId))
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
      bool isAdmin = User.IsInRole("Admin");
      if (!isPM && !isAdmin) return Forbid();
      if (ewp.WorkPackageProjectId != projectId) return BadRequest();
      var result = _workPackageService.AssignResponsibleEngineer(ewp);
      if (result == null) return BadRequest();
      return Ok(result);
    }
  }
}
