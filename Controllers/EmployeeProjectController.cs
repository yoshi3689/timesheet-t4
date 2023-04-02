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
            public List<Project>? Projects { get; set; }
            public List<ApplicationUser>? Users { get; set; }
        }

        // GET: EmployeeProject
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

        // GET: EmployeeProject/Details/5
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult AddEmployee([FromBody] List<EmployeeProject> employeeProjects)
        {
            foreach (var ep in employeeProjects)
            {
                if (ep.ProjectId != 010)
                {
                    _context.Add(ep);
                }
            }
            _context.SaveChanges();
            return Json("a");
        }

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


        // GET: EmployeeProject/Edit/5
        [Authorize(Policy = "KeyRequirement")]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
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
