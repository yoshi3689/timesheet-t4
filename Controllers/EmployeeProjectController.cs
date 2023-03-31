using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
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

        // GET: EmployeeProject
        [Authorize(Policy = "KeyRequirement")]

        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.EmployeeProjects.Include(e => e.Project).Include(e => e.User);
            return View(await applicationDbContext.ToListAsync());
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
        [Authorize(Policy = "KeyRequirement")]

        public IActionResult Create()
        {
            ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "ProjectId");
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: EmployeeProject/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,ProjectId")] EmployeeProject employeeProject)
        {
            EmployeeProject ep = new EmployeeProject
            {
                UserId = employeeProject.UserId,
                ProjectId = employeeProject.ProjectId
            };

            _context.EmployeeProjects.Add(ep);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));

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

        // POST: EmployeeProject/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
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
