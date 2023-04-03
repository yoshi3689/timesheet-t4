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
      public List<Project> Projects { get; set; }
      public List<ApplicationUser> Users { get; set; }
    }

    /// <summary>
    /// displays a list of employees in projects
    /// </summary>
    /// <returns>lists of projects and employees</returns>
    [Authorize(Policy = "KeyRequirement")]
    public async Task<IActionResult> Index()
    {
      var Users = _context.Users.Include(u => u.EmployeeProjects).ToList();
      ViewBag.TSA = Users.Find(u => u.TimesheetApproverId != null);
      var Projects = _context.Projects.Include(p => p.EmployeeProjects).ToList();
      return View(new ProjectUsers { Projects = Projects, Users = Users });
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
    public async Task<IActionResult> AddEmployee([FromBody] List<EmployeeProject> employeeProjects)
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
    public async Task<IActionResult> AssignTSApprover([FromBody] EmployeeProject employeeProject)
    {
      // find a user with the Id
      var futureTSA = _context.Users.Find(employeeProject.UserId);
      // check if there is already a TSA by querying
      var potentialTSA = _context.Users.Where(u => u.TimesheetApproverId != null);
      // if TSA already exists
      if (potentialTSA.Count() != 0)
      {
        // remove the current TSA
        potentialTSA!.FirstOrDefault()!.TimesheetApproverId = null;
        Console.WriteLine(potentialTSA.FirstOrDefault().TimesheetApproverId);
      }
      // set a new TSA
      futureTSA!.TimesheetApproverId = employeeProject.UserId;
      Console.WriteLine(futureTSA!.TimesheetApproverId);
      _context.SaveChanges();
      return new JsonResult(futureTSA.FirstName + " " + futureTSA.LastName);
    }
  }
}
