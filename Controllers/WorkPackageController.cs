using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Index()
        {
            // Console.WriteLine(User.Identity.Name);
            ApplicationUser user = (await _userManager.GetUserAsync(User))!;
            // var applicationDbContext = _context.WorkPackages.Include(w => w.ParentWorkPackage).Include(w => w.Project).Include(w => w.ResponsibleUser);
            ViewData["userId"] = user.Id;
            // fetch if the user is assigned to the wp as an novice emp or a RE
            var applicationDbContext
              = _context.WorkPackages.Where(wp =>
                (wp!.EmployeeWorkPackages!.Select(ewp => ewp.UserId).Contains(user.Id)
                || user.Id == wp.ResponsibleUserId)
                && wp.IsBottomLevel).Include(w => w.Project);

            return View(await applicationDbContext.ToListAsync());
        }

        [Authorize(Policy = "KeyRequirement")]
        public IActionResult CreateBudgetsAndEstimates(LowestWorkPackageBAndEViewModel input)
        {
            // add a new set of budgets
            if (input.budgets != null)
            {
                Budget? parentB = null;
                List<Budget> parentBudgets = _context.Budgets.Where(c => c.WPProjectId == input.budgets[0].WPProjectId).ToList();
                foreach (var budget in input.budgets)
                {
                    Budget newBudget = new Budget
                    {
                        WPProjectId = input.budgets[0].WPProjectId,
                        People = budget.People,
                        Days = budget.Days,
                        LabourCode = budget.LabourCode,
                        UnallocatedDays = budget.UnallocatedDays,
                        UnallocatedPeople = budget.UnallocatedPeople,
                        isREBudget = true
                    };
                    _context.Budgets!.Add(newBudget);
                    parentB = parentBudgets.Where(c => c.LabourCode == budget.LabourCode).First();
                    if (parentB != null)
                    {
                        parentB.UnallocatedDays -= newBudget.UnallocatedDays;
                        parentB.UnallocatedPeople -= newBudget.UnallocatedPeople;
                    }
                }
            }

            if (input.estimates != null)
            {
                foreach (var estimate in input.estimates)
                {
                    ResponsibleEngineerEstimate re = new ResponsibleEngineerEstimate
                    {
                        WPProjectId = estimate.WPProjectId,
                        LabourCode = estimate.LabourCode,
                        // Date will be set after form submission
                        Date = DateOnly.FromDateTime(DateTime.Now),
                        EstimatedCost = estimate.EstimatedCost,
                    };
                    _context.ResponsibleEngineerEstimates!.Add(re);
                }
                _context.SaveChanges();
            }
            return Json(input);
        }

        // GET: WorkPackage/Details/5
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Edit(string id1, int id2)
        {
            // fetch project budgets for this LWP set by PM
            List<Budget> budgets
            = _context.Budgets.Where(b => b.WPProjectId == id2 + "~" + id1)
            .AsEnumerable().TakeLast(8).ToList();
            // fetch REEstimates for this LWP
            List<ResponsibleEngineerEstimate> estimates
            = _context.ResponsibleEngineerEstimates.Where(ree => ree.WPProjectId == id2 + "~" + id1)
            .AsEnumerable().TakeLast(8).ToList();

            // if no estimates made for this LWP or the date of the most recently created
            // set of estimates is more than 7 days prior to the current date
            bool shouldMakeWE = estimates.Count == 0
              || DateOnly.FromDateTime(DateTime.Now).DayNumber
                - estimates[estimates.Count - 1].Date.GetValueOrDefault().DayNumber >= 7;

            // for now, clear the content of the estimates array
            // just to send the new set of re objects to the form
            estimates.Clear();
            for (int i = 0; i < 8; i++)
            {
                budgets[i].Days = 0;
                budgets[i].People = 0;
                ResponsibleEngineerEstimate re = new ResponsibleEngineerEstimate
                {
                    WPProjectId = id2 + "~" + id1,
                    LabourCode = budgets[i].LabourCode,
                    // Date will be set after form submission
                    Date = null,
                    EstimatedCost = 0,
                };
                estimates.Add(re);
            }

            LowestWorkPackageBAndEViewModel model = new LowestWorkPackageBAndEViewModel
            {
                budgets = budgets,
                estimates = shouldMakeWE ? estimates : null,
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
        [Authorize(Policy = "KeyRequirement")]
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
        [Authorize(Policy = "KeyRequirement")]
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
