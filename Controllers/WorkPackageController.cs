using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers
{
    public class WorkPackageController : Controller
    {
        private readonly IWorkPackageService _workPackageService;
        private readonly UserManager<ApplicationUser> _userManager;

        public WorkPackageController(IWorkPackageService workPackageService, UserManager<ApplicationUser> userManager)
        {
            _workPackageService = workPackageService;
            _userManager = userManager;
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Index()
        {
            ApplicationUser user = (await _userManager.GetUserAsync(User))!;
            ViewData["name"] = user.FirstName;
            var wps = await _workPackageService.GetResponsibleWorkPackagesAsync(user.Id);
            return View(wps);
        }

        [Authorize(Policy = "KeyRequirement")]
        public IActionResult CreateBudgetsAndEstimates(LowestWorkPackageBAndEViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", input);
            }
            _workPackageService.CreateBudgetsAndEstimates(input);
            return RedirectToAction("Index");
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var workPackage = await _workPackageService.GetWorkPackageDetailsAsync(id);
            if (workPackage == null)
            {
                return NotFound();
            }

            return View(workPackage);
        }

        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Edit(string id1, int id2)
        {
            var model = _workPackageService.GetEditModel(id1, id2);
            return View(model);
        }
    }
}
