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
