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

        /// <summary>
        /// Get a page that contains a list of all the work packages an employee is responsible for.
        /// </summary>
        /// <returns>page of wps</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Index()
        {
            ApplicationUser user = (await _userManager.GetUserAsync(User))!;
            ViewData["name"] = user.FirstName;
            // fetch if the user is assigned to the wp as an novice emp or a RE
            var applicationDbContext = _context.WorkPackages.Where(wp => user.Id == wp.ResponsibleUserId && wp.IsBottomLevel).Include(w => w.Project);
            return View(await applicationDbContext.ToListAsync());
        }

        /// <summary>
        /// Endpoint to create the budget and estimmates.
        /// </summary>
        /// <param name="input">Takes model containing new budget and estimates</param>
        /// <returns>redirection to index if successful</returns>
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult CreateBudgetsAndEstimates(LowestWorkPackageBAndEViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", input);
            }
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
                    int offset = (7 - (int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday) % 7;
                    DateTime nextFriday = DateTime.Today.AddDays(offset);
                    ResponsibleEngineerEstimate re = new ResponsibleEngineerEstimate
                    {
                        WPProjectId = estimate.WPProjectId,
                        LabourCode = estimate.LabourCode,
                        Date = DateOnly.FromDateTime(nextFriday),
                        EstimatedCost = estimate.EstimatedCost,
                    };
                    _context.ResponsibleEngineerEstimates!.Add(re);
                }
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Get the details about a work package
        /// </summary>
        /// <param name="id">id of wp to get details for</param>
        /// <returns>page with details</returns>
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


        /// <summary>
        /// Gets the page to create the original budget and weekly estimates.
        /// </summary>
        /// <param name="id1">work package id</param>
        /// <param name="id2">project id</param>
        /// <returns>page to create budget and estimates</returns>
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Edit(string id1, int id2)
        {
            int labourGradeCount = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).Count();
            // fetch project budgets for this LWP set by PM
            List<Budget> budgets = _context.Budgets.Where(b => b.WPProjectId == id2 + "~" + id1 && b.isREBudget == true).AsEnumerable().TakeLast(labourGradeCount).ToList();
            if (budgets.Count() == 0)
            {
                budgets = _context.Budgets.Where(b => b.WPProjectId == id2 + "~" + id1 && b.isREBudget == false).AsEnumerable().TakeLast(labourGradeCount).ToList();
            }
            // fetch REEstimates for this LWP
            List<ResponsibleEngineerEstimate> estimates = _context.ResponsibleEngineerEstimates.Where(ree => ree.WPProjectId == id2 + "~" + id1).OrderByDescending(c => c.Date)
                .Take(labourGradeCount).ToList();

            // if no estimates made for this LWP or the date of the most recently created
            // make sure there aren't any with the same end date
            int offset = (7 - (int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday) % 7;
            DateTime nextFriday = DateTime.Today.AddDays(offset);
            bool shouldMakeWE = estimates.Count == 0 || DateOnly.FromDateTime(nextFriday) != estimates[0].Date;
            // for now, clear the content of the estimates array
            // just to send the new set of re objects to the form
            estimates.Clear();
            for (int i = 0; i < labourGradeCount; i++)
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

            var lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
            foreach (var item in budgets)
            {
                item.Rate = lgs.Where(c => c.LabourCode == item.LabourCode).First().Rate;
            }

            LowestWorkPackageBAndEViewModel model = new LowestWorkPackageBAndEViewModel
            {
                budgets = budgets,
                estimates = shouldMakeWE ? estimates : null,
            };
            return View(model);
        }

        /// <summary>
        /// check if a wp exists
        /// </summary>
        /// <param name="id">wp id</param>
        /// <returns>true if exists</returns>
        private bool WorkPackageExists(string id)
        {
            return (_context.WorkPackages?.Any(e => e.WorkPackageId == id)).GetValueOrDefault();
        }
    }
}
