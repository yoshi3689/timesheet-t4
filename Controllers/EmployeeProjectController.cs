using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers
{
    public class EmployeeProjectController : Controller
    {
        private readonly IEmployeeService _employeeService;

        public EmployeeProjectController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        public class ProjectUsers
        {
            public List<Project>? Projects { get; set; }
            public List<ApplicationUser>? Users { get; set; }
        }

        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Index()
        {
            var user = _employeeService.GetCurrentUserWithSupervisedUsers(User.Identity!.Name!);
            if (user == null)
            {
                return View();
            }
            if (user.SupervisedUsers.Where(c => c.SupervisorId != c.TimesheetApproverId).FirstOrDefault() == null)
            {
                ViewBag.TSA = null;
            }
            else
            {
                ViewBag.TSA = user.SupervisedUsers.Where(c => c.SupervisorId == c.TimesheetApproverId).FirstOrDefault();
            }
            var options = user.SupervisedUsers.ToList();
            options.Add(user);
            var projects = _employeeService.GetAllProjectsWithEmployees();
            return View(new ProjectUsers { Projects = projects, Users = options });
        }

        [HttpGet]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Create(int ProjectId)
        {
            var user = _employeeService.GetCurrentUserWithSupervisedUsers(User.Identity!.Name!);
            if (user == null) return BadRequest();

            var usersAvailable = _employeeService.GetAvailableUsersForProject(ProjectId, user.Id, user.Id, User.IsInRole("Admin"));
            ViewData["Users"] = usersAvailable;
            return View(usersAvailable);
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult AddEmployee([FromBody] List<EmployeeProject> employeeProjects)
        {
            _employeeService.AddEmployeesToProject(employeeProjects);
            return new JsonResult(employeeProjects);
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult AssignTSApprover([FromBody] EmployeeProject employeeProject)
        {
            var user = _employeeService.GetCurrentUserWithSupervisedUsers(User.Identity!.Name!);
            var futureTSA = _employeeService.FindEmployeeAsync(employeeProject.UserId!).GetAwaiter().GetResult();

            if (user == null || futureTSA == null || (futureTSA.SupervisorId != user.Id && futureTSA.Id != user.Id))
            {
                return BadRequest();
            }

            _employeeService.AssignTimesheetApprover(user, employeeProject.UserId!);
            return Json("a");
        }
    }
}
