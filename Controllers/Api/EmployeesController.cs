using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.DTOs.Employees;
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
        private readonly IProjectService _projectService;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeesController(
            IEmployeeService employeeService,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            RoleManager<IdentityRole> roleManager)
        {
            _employeeService = employeeService;
            _userManager = userManager;
            _projectService = projectService;
            _roleManager = roleManager;
        }

        // GET /api/employees?page=1&pageSize=50
        [HttpGet]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50)
        {
            var users = await _employeeService.GetAllEmployeesPaginatedAsync(page, pageSize);
            int totalCount = _employeeService.GetTotalEmployeeCount();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var dtos = users.Select(u => new EmployeeSummaryDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                EmployeeNumber = u.EmployeeNumber,
                JobTitle = u.JobTitle,
                LabourGradeCode = u.LabourGradeCode,
                SupervisorId = u.SupervisorId,
                TimesheetApproverId = u.TimesheetApproverId,
            });
            return Ok(new { users = dtos, totalPages, currentPage = page, pageSize });
        }

        // GET /api/employees/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _employeeService.GetEmployeeDetailsAsync(id);
            if (user == null) return NotFound();

            bool isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");
            string callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (isAdminOrHR || callerId == id)
            {
                return Ok(new EmployeeDetailDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    EmployeeNumber = user.EmployeeNumber,
                    JobTitle = user.JobTitle,
                    LabourGradeCode = user.LabourGradeCode,
                    SupervisorId = user.SupervisorId,
                    TimesheetApproverId = user.TimesheetApproverId,
                    Email = user.Email,
                    SickDays = user.SickDays,
                    FlexTime = user.FlexTime,
                    Overtime = user.Overtime,
                    Salary = user.Salary,
                });
            }

            return Ok(new EmployeeSummaryDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                EmployeeNumber = user.EmployeeNumber,
                JobTitle = user.JobTitle,
                LabourGradeCode = user.LabourGradeCode,
                SupervisorId = user.SupervisorId,
                TimesheetApproverId = user.TimesheetApproverId,
            });
        }

        // POST /api/employees
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            bool isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");
            if (!isAdminOrHR) return Forbid();

            // HR cannot elevate anyone to Admin — only Admin can assign the Admin role
            bool callerIsAdmin = User.IsInRole("Admin");
            if (!callerIsAdmin && dto.Roles.Contains("Admin"))
                return Forbid();

            // Validate all requested roles exist
            foreach (var role in dto.Roles)
                if (!await _roleManager.RoleExistsAsync(role))
                    return BadRequest(new { message = $"Role '{role}' does not exist." });

            try
            {
                var (user, tempPassword) = await _employeeService.CreateEmployeeAsync(dto, _userManager);
                return Created($"/api/employees/{user.Id}", new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    employeeNumber = user.EmployeeNumber,
                    jobTitle = user.JobTitle,
                    labourGradeCode = user.LabourGradeCode,
                    tempPassword
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /api/employees/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateEmployeeDto dto)
        {
            if (id != dto.Id) return BadRequest();

            string callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            bool isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");

            if (!isAdminOrHR && callerId != id)
                return Forbid();

            var existing = await _employeeService.FindEmployeeAsync(id);
            if (existing == null) return NotFound();
            await _employeeService.UpdateEmployeeAsync(existing, dto, isAdminOrHR, _userManager);
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
        public async Task<IActionResult> AddToProject([FromBody] List<EmployeeProject> employeeProjects)
        {
            string callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            foreach (int projectId in employeeProjects.Select(ep => ep.ProjectId).Distinct())
            {
                bool isPM = await _projectService.VerifyProjectManagerAsync(projectId, callerId);
                if (!isPM) return Forbid();
            }
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
