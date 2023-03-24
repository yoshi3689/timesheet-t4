using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Controllers
{
    public class WorkPackageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WorkPackageController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: WorkPackage
        public async Task<IActionResult> Index()
        {
            // Console.WriteLine(User.Identity.Name);
            ApplicationUser user = (await _userManager.GetUserAsync(User))!;
            // var applicationDbContext = _context.WorkPackages.Include(w => w.ParentWorkPackage).Include(w => w.Project).Include(w => w.ResponsibleUser);
            ViewData["userId"] = user.Id;
            // fetch if the user is assigned to the wp as an novice emp or a RE
            var applicationDbContext = _context.WorkPackages.Where(wp => (wp!.EmployeeWorkPackages!.Select(ewp => ewp.UserId).Contains(user.Id) || user.Id == wp.ResponsibleUserId) && wp.IsBottomLevel).Include(w => w.Project);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: WorkPackage/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null || _context.WorkPackages == null)
            {
                return NotFound();
            }

            var workPackage = await _context.WorkPackages
                .Include(w => w.ParentWorkPackage)
                .Include(w => w.Project)
                .Include(w => w.ResponsibleUser)
                .FirstOrDefaultAsync(m => m.WorkPackageId == id);
            if (workPackage == null)
            {
                return NotFound();
            }

            return View(workPackage);
        }

        // GET: WorkPackage/Create
        public IActionResult Create()
        {
            ViewData["ParentWorkPackageId"] = new SelectList(_context.WorkPackages, "WorkPackageId", "WorkPackageId");
            ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "ProjectManagerId");
            ViewData["ResponsibleUserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: WorkPackage/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("WorkPackageId,ProjectId,Title,ResponsibleUserId,ParentWorkPackageId,ParentWorkPackageProjectId,IsBottomLevel,ActualCost,IsClosed")] WorkPackage workPackage)
        {
            if (ModelState.IsValid)
            {
                _context.Add(workPackage);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ParentWorkPackageId"] = new SelectList(_context.WorkPackages, "WorkPackageId", "WorkPackageId", workPackage.ParentWorkPackageId);
            ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "ProjectManagerId", workPackage.ProjectId);
            ViewData["ResponsibleUserId"] = new SelectList(_context.Users, "Id", "Id", workPackage.ResponsibleUserId);
            return View(workPackage);
        }

        // GET: WorkPackage/Edit/5
        public async Task<IActionResult> Edit(string id1, int id2)
        {
            // get a project budget
            // var projectBudget = _context.Budgets.Where(c => c.WPProjectId == projectId + "~0").ToList();
            List<Budget> budgets
            = _context.Budgets.Where(b => b.WPProjectId == id2 + "~" + id1).ToList();
            List<ResponsibleEngineerEstimate> estimates
            = _context.ResponsibleEngineerEstimates.Where(ree => ree.WPProjectId == id2 + "~" + id1).ToList();
            
            bool isREBudgetAlreadyCreated = budgets.Count > 8;
            foreach (var item in budgets)
            {
                item.Days = 0;
                item.People = 0;
            }
            LowestWorkPackageBAndEViewModel model = new LowestWorkPackageBAndEViewModel
            {
                budgets = isREBudgetAlreadyCreated ? null : budgets,
                estimates = estimates
            };
            
            return View(model);
        }

        // POST: WorkPackage/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        // [HttpPost]
        // [ValidateAntiForgeryToken]
        // public async Task<IActionResult> Edit(string id, [Bind("WorkPackageId,ProjectId,Title,ResponsibleUserId,ParentWorkPackageId,ParentWorkPackageProjectId,IsBottomLevel,ActualCost,IsClosed")] WorkPackage workPackage)
        // {
        //     if (id != workPackage.WorkPackageId)
        //     {
        //         return NotFound();
        //     }

        //     if (ModelState.IsValid)
        //     {
        //         try
        //         {
        //             _context.Update(workPackage);
        //             await _context.SaveChangesAsync();
        //         }
        //         catch (DbUpdateConcurrencyException)
        //         {
        //             if (!WorkPackageExists(workPackage.WorkPackageId))
        //             {
        //                 return NotFound();
        //             }
        //             else
        //             {
        //                 throw;
        //             }
        //         }
        //         return RedirectToAction(nameof(Index));
        //     }
        //     ViewData["ParentWorkPackageId"] = new SelectList(_context.WorkPackages, "WorkPackageId", "WorkPackageId", workPackage.ParentWorkPackageId);
        //     ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "ProjectManagerId", workPackage.ProjectId);
        //     ViewData["ResponsibleUserId"] = new SelectList(_context.Users, "Id", "Id", workPackage.ResponsibleUserId);
        //     return View(workPackage);
        // }

        // GET: WorkPackage/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null || _context.WorkPackages == null)
            {
                return NotFound();
            }

            var workPackage = await _context.WorkPackages
                .Include(w => w.ParentWorkPackage)
                .Include(w => w.Project)
                .Include(w => w.ResponsibleUser)
                .FirstOrDefaultAsync(m => m.WorkPackageId == id);
            if (workPackage == null)
            {
                return NotFound();
            }

            return View(workPackage);
        }

        // POST: WorkPackage/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (_context.WorkPackages == null)
            {
                return Problem("Entity set 'ApplicationDbContext.WorkPackages'  is null.");
            }
            var workPackage = await _context.WorkPackages.FindAsync(id);
            if (workPackage != null)
            {
                _context.WorkPackages.Remove(workPackage);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool WorkPackageExists(string id)
        {
            return (_context.WorkPackages?.Any(e => e.WorkPackageId == id)).GetValueOrDefault();
        }
    }
}
