using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Controllers
{
    /// <summary>
    /// This class is used as a way to assign employees to projects. This can be done by supervisors.
    /// </summary>
    public class EmployeeProjectController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeProjectController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// This inner class to help list out all the employees per project
        /// including projects without employees
        /// </summary>
        public class ProjectUsers
        {
            public List<Project>? Projects { get; set; }
            public List<ApplicationUser>? Users { get; set; }
        }

        /// <summary>
        /// displays a list of employees in projects
        /// </summary>
        /// <returns>lists of projects and employees</returns>
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Index()
        {
            var user = _context.Users.Where(c => c.UserName == User.Identity!.Name).Include(c => c.SupervisedUsers).FirstOrDefault();
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
            var Projects = _context.Projects.Where(c => c.ProjectId != 010).Include(p => p.EmployeeProjects).ThenInclude(c => c.User).ToList();
            return View(new ProjectUsers { Projects = Projects, Users = options });
        }


        /// <summary>
        /// displays a page where a user can assign employee(s) to a project specified by the id
        /// </summary>
        /// <param name="ProjectId">project id of a project to display</param>
        /// <returns>users available in a project</returns>
        [HttpGet]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Create(int ProjectId)
        {
            var Project = _context.Projects.Find(ProjectId);
            var UsersInProject = _context.EmployeeProjects.Where(ep => ep.ProjectId == ProjectId).Select(ep => ep.UserId).ToList();
            var UsersAvailable = _context.Users.Where(u => !UsersInProject.Contains(u.Id)).ToList();
            ViewData["Users"] = UsersAvailable;
            return View(UsersAvailable);
        }

        /// <summary>
        /// add employee(s) to a project
        /// </summary>
        /// <param name="employeeProjects">list of employee to project mappings to add the mapped employees</param>
        /// <returns>list of employees added</returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult AddEmployee([FromBody] List<EmployeeProject> employeeProjects)
        {
            foreach (var ep in employeeProjects)
            {
                _context.Add(ep);
            }
            _context.SaveChanges();
            return new JsonResult(employeeProjects);
        }

        /// <summary>
        /// assign a TS approver
        /// </summary>
        /// <param name="employeeProject">employee project mapping of an employee to be assigned as a TS approver</param>
        /// <returns>the name of the new TS approver</returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult AssignTSApprover([FromBody] EmployeeProject employeeProject)
        {
            var user = _context.Users.Where(c => c.UserName == User.Identity!.Name).Include(c => c.SupervisedUsers).FirstOrDefault();
            var futureTSA = _context.Users.Find(employeeProject.UserId);

            if (user == null || futureTSA == null || (futureTSA.SupervisorId != user.Id && futureTSA.Id != user.Id))
            {
                return BadRequest();
            }
            // set all the supervised users approver to the new person
            foreach (var s in user.SupervisedUsers)
            {
                s.TimesheetApproverId = employeeProject.UserId;
            }
            // their approver shouldn't be themself, so keep as the supervisor
            futureTSA.TimesheetApproverId = user.Id;
            _context.SaveChanges();
            return Json("a");
        }
    }
}
