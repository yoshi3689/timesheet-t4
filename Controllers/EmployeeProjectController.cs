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

    public class ProjectUsers
    {
      public List<Project> Projects { get; set; }
      public List<ApplicationUser> Users { get; set; }
    }
    // GET: EmployeeProject
    public async Task<IActionResult> Index()
    {
      var Users = _context.Users.Include(u => u.EmployeeProjects).ToList();
      ViewBag.TSA = Users.Find(u => u.TimesheetApproverId != null);
      var Projects = _context.Projects.Include(p => p.EmployeeProjects).ToList();
      return View(new ProjectUsers { Projects = Projects, Users = Users });
    }

    // GET: EmployeeProject/Details/5
    public async Task<IActionResult> Details(string id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var employeeProject = await _context.EmployeeProjects
          .Include(e => e.Project)
          .Include(e => e.User)
          .FirstOrDefaultAsync(m => m.UserId == id);
      if (employeeProject == null)
      {
        return NotFound();
      }

      return View(employeeProject);
    }

    // GET: EmployeeProject/Create
    // accept a ProjectId 
    [HttpGet]
    public IActionResult Create(int ProjectId)
    {
      var Project = _context.Projects.Find(ProjectId);
      var UsersInProject = _context.EmployeeProjects.Where(ep => ep.ProjectId == ProjectId).Select(ep => ep.UserId).ToList();
      var UsersAvailable = _context.Users.Where(u => !UsersInProject.Contains(u.Id)).ToList();
      ViewData["Users"] = UsersAvailable;
      return View(UsersAvailable);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddEmployee([FromBody] List<EmployeeProject> employeeProjects)
    {
      Console.WriteLine(employeeProjects.Count);
      foreach (var ep in employeeProjects)
      {
        _context.Add(ep);
      }
      _context.SaveChanges();
      return Json("a");
    }

    [HttpPost]
    [Authorize]
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
      return Json("a");
    }


    // GET: EmployeeProject/Edit/5
    public async Task<IActionResult> Edit(string id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var employeeProject = await _context.EmployeeProjects.FindAsync(id);
      if (employeeProject == null)
      {
        return NotFound();
      }
      ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "ProjectId", employeeProject.ProjectId);
      ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", employeeProject.UserId);
      return View(employeeProject);
    }

    // POST: EmployeeProject/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("UserId,ProjectId")] EmployeeProject employeeProject)
    {
      if (id != employeeProject.UserId)
      {
        return NotFound();
      }

      if (ModelState.IsValid)
      {
        try
        {
          _context.Update(employeeProject);
          await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
          if (!EmployeeProjectExists(employeeProject.UserId))
          {
            return NotFound();
          }
          else
          {
            throw;
          }
        }
        return RedirectToAction(nameof(Index));
      }
      ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "ProjectId", employeeProject.ProjectId);
      ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", employeeProject.UserId);
      return View(employeeProject);
    }

    // GET: EmployeeProject/Delete/5
    public async Task<IActionResult> Delete(string id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var employeeProject = await _context.EmployeeProjects
          .Include(e => e.Project)
          .Include(e => e.User)
          .FirstOrDefaultAsync(m => m.UserId == id);
      if (employeeProject == null)
      {
        return NotFound();
      }

      return View(employeeProject);
    }

    // POST: EmployeeProject/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
      var employeeProject = await _context.EmployeeProjects.FindAsync(id);
      if (employeeProject != null)
      {
        _context.EmployeeProjects.Remove(employeeProject);
      }
      await _context.SaveChangesAsync();
      return RedirectToAction(nameof(Index));
    }

    private bool EmployeeProjectExists(string id)
    {
      return _context.EmployeeProjects.Any(e => e.UserId == id);
    }

  }
}
