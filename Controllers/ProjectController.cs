using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// report generation packages
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System.Text.Json;

using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TimesheetApp.Helpers;

namespace TimesheetApp.Controllers
{
    /// <summary>
    /// Deals with creating and managing projects, which includes creating and updating work packages.
    /// </summary>
    [Authorize(Policy = "KeyRequirement")]

    public class ProjectController : Controller
    {
        private readonly ILogger<ProjectController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private int? CurrentProject;
        public ProjectController(ILogger<ProjectController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Used to get the main projects list. If you are an admin or HR, you can see all of them.
        /// </summary>
        /// <returns>project list page</returns>
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Index()
        {
            if (User.Identity!.IsAuthenticated && (User.IsInRole("HR") || User.IsInRole("Admin")))
            {
                var projects = _context.Projects!.Where(p => p.ProjectId != 10).Include(s => s.ProjectManager).OrderBy(c => c.ProjectId);
                return View(projects);
            }
            else
            {
                var userId = _userManager.GetUserId(HttpContext.User);
                var project = _context.Projects!.Where(s => (s.ProjectManager!.Id == userId || s.AssistantProjectManagerId == userId) && s.ProjectId != 10).OrderBy(c => c.ProjectId).Include(s => s.ProjectManager);
                return View(project);
            }
        }

        /// <summary>
        /// Gets the page for creating a new project. Only HR or admin may create a project.
        /// </summary>
        /// <returns>new project page.</returns>
        [Authorize(Policy = "KeyRequirement", Roles = "HR,Admin")]
        public IActionResult Create()
        {
            var users = _context.Users.ToList();
            ViewData["users"] = users;
            CreateProjectViewModel proj = new CreateProjectViewModel
            {
                budgets = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).Select(lg => new Budget
                {
                    LabourCode = lg.LabourCode,
                    isREBudget = false,
                    Rate = lg.Rate,
                }).ToList()
            };
            return View(proj);
        }

        /// <summary>
        /// Returns the page which is used to manage the project, including creation of work packages.
        /// </summary>
        /// <param name="id">the id of the project</param>
        /// <returns>The project manage page</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == 10)
            {
                return RedirectToAction("Index");
            }
            //store the current project into the session for use later.
            HttpContext.Session.SetInt32("CurrentProject", id ?? 0);
            CurrentProject = id;
            if (await verifyPMAsync() is IActionResult isPM) return isPM;


            //find all work packages for the project and include the children so a tree can be made
            var workpackages = _context.WorkPackages!.Where(c => c.ProjectId == id).Include(c => c.ResponsibleUser).Include(c => c.ParentWorkPackage).Include(c => c.ChildWorkPackages).Include(c => c.Project);
            var top = workpackages.FirstOrDefault(c => c.ParentWorkPackage == null)!;

            WorkPackageViewModel model = new WorkPackageViewModel
            {
                wps = findAllChildren(top),
                LabourGrades = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList()
            };
            //calculate the total budget amount for each work package.
            List<Budget> budgets = _context.Budgets.Where(c => c.WPProjectId.StartsWith(CurrentProject + "~")).ToList();
            model.wps = getTotalMoney(model.wps, budgets);
            return View(model);
        }

        /// <summary>
        /// Calculate the total budget and total remaining fields, for viewing on the front end.
        /// </summary>
        /// <param name="wps">a list of work packages that need their fields update</param>
        /// <param name="budgets">a list of the budgets to get the values to update the fields</param>
        /// <returns>a list of work packages with totalBudget and totalRemaining filled in</returns>
        private List<WorkPackage> getTotalMoney(List<WorkPackage> wps, List<Budget> budgets)
        {
            List<LabourGrade> lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
            foreach (var wp in wps)
            {
                double total = 0;
                double remaining = 0;
                foreach (var lg in lgs)
                {
                    var budget = budgets.Where(c => c.WPProjectId == (wp.ProjectId + "~" + wp.WorkPackageId) && c.LabourCode == lg.LabourCode).First();
                    total += budget.BudgetAmount * lg.Rate;
                    remaining += budget.UnallocatedDays * budget.UnallocatedPeople * lg.Rate;
                }
                wp.TotalBudget = total;
                wp.TotalRemaining = remaining;
            }
            return wps;
        }

        /// <summary>
        /// Uses DFS to create a list of WPs that can be created from the top down. The parents need to come first so the children can be inserted as children.
        /// </summary>
        /// <param name="top">top of the "tree" or highest level WP</param>
        /// <returns>a list in usable order</returns>
        private List<WorkPackage> findAllChildren(WorkPackage top)
        {
            List<WorkPackage> wps = new List<WorkPackage>();
            wps.Add(top);
            if (top.ChildWorkPackages == null || top.ChildWorkPackages.Count() == 0)
            {
                top = _context.WorkPackages!.Include(c => c.ChildWorkPackages).Include(c => c.ResponsibleUser).Include(c => c.Project).FirstOrDefault(c => c.ProjectId == top.ProjectId && c.WorkPackageId == top.WorkPackageId)!;
            }
            if (top.ChildWorkPackages != null && top.ChildWorkPackages.Count() != 0)
            {
                foreach (var wp in top.ChildWorkPackages)
                {
                    foreach (var item in findAllChildren(wp))
                    {
                        wps.Add(item);
                    }
                }
            }
            return wps;
        }

        /// <summary>
        /// Used to assign employees to work packages.
        /// </summary>
        /// <param name="ewps">A list of employee WP relationships.</param>
        /// <returns>A list of the added users.</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> AssignEmployeesAsync([FromBody] List<EmployeeWorkPackage> ewps)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;
            if (ewps.Count == 0)
            {
                return BadRequest();
            }
            var workPackageId = ewps[0].WorkPackageId;
            var workPackageProjectId = ewps[0].WorkPackageProjectId;

            //only allow one work package per request
            for (int i = 1; i < ewps.Count; i++)
            {
                if (ewps[i].WorkPackageId != workPackageId || ewps[i].WorkPackageProjectId != workPackageProjectId)
                {
                    return BadRequest();
                }
            }

            var oldWp = _context.WorkPackages.Where(c => ewps.Select(s => s.WorkPackageId).Contains(c.WorkPackageId) && c.IsBottomLevel == false).ToList();
            if (oldWp.Any()) return Json("Error");

            var currentWp = _context.WorkPackages.Where(c => c.WorkPackageId == workPackageId).Include(c => c.Project).First();
            if (currentWp.IsClosed)
            {
                return BadRequest();
            }

            var removedEmployeeIds = _context.EmployeeWorkPackages.Where(c => c.WorkPackageId == workPackageId && c.WorkPackageProjectId == workPackageProjectId).Select(c => c.UserId).ToList();
            _context.EmployeeWorkPackages.RemoveRange(_context.EmployeeWorkPackages.Where(c => c.WorkPackageId == workPackageId && c.WorkPackageProjectId == workPackageProjectId));

            var addedEmployeeIds = ewps.Where(e => e.UserId != null).Select(e => e.UserId).ToList();

            var notifiedAddedEmployeeIds = addedEmployeeIds.Except(removedEmployeeIds).ToList();
            var notifiedRemovedEmployeeIds = removedEmployeeIds.Except(addedEmployeeIds).ToList();

            _context.EmployeeWorkPackages.AddRange(ewps.Where(e => e.UserId != null));

            var workPackageString = workPackageProjectId + "~" + workPackageId;

            //send appropriate notifications
            foreach (var notifiedEmployeeId in notifiedAddedEmployeeIds)
            {
                if (_context.Users.Any(e => e.Id == notifiedEmployeeId) && !_context.Notifications.Any(n => n.For == workPackageString + " Add"))
                {
                    _context.Notifications.Add(new Notification { UserId = notifiedEmployeeId, Message = "You have been added to the work package " + currentWp.Title + " in the project " + currentWp.Project!.ProjectTitle, For = workPackageString + " Add", Importance = 1 });
                }
            }

            foreach (var notifiedEmployeeId in notifiedRemovedEmployeeIds)
            {
                if (_context.Users.Any(e => e.Id == notifiedEmployeeId) && !_context.Notifications.Any(n => n.For == workPackageString + " Remove"))
                {
                    _context.Notifications.Add(new Notification { UserId = notifiedEmployeeId, Message = "You have been removed from the work package " + currentWp.Title + " in the project " + currentWp.Project!.ProjectTitle, For = workPackageString + " Remove", Importance = 2 });
                }
            }

            _context.SaveChanges();

            return Json(_context.Users.Where(c => addedEmployeeIds.Contains(c.Id)).Select(e => new { e.Id, e.FirstName, e.LastName, e.JobTitle }!));
        }


        /// <summary>
        /// Used to assign REs to a WP
        /// </summary>
        /// <param name="ewp">Takes the Employee-WP relationship of the RE</param>
        /// <returns>RE's full name</returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> AssignResponsibleEngineerAsync([FromBody] EmployeeWorkPackage ewp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            if (ewp != null)
            {
                var LLWP = await _context.WorkPackages.FindAsync(ewp.WorkPackageId, ewp.WorkPackageProjectId);
                if (LLWP != null)
                {
                    if (LLWP.IsClosed)
                    {
                        return BadRequest();
                    }
                    var oldRE = LLWP.ResponsibleUserId;
                    LLWP.ResponsibleUserId = ewp.UserId;
                    // add rows of estimate for this LLWP
                    var user = _context.Users.Where(c => c.Id == ewp.UserId).FirstOrDefault();
                    if (user == null) return new JsonResult("Error!");
                    ewp = _context.EmployeeWorkPackages.Where(c => c.UserId == ewp.UserId && c.WorkPackageId == ewp.WorkPackageId).Include(c => c.WorkPackage).Include(c => c.WorkPackage!.Project).First();
                    var workPackageString = ewp.WorkPackageProjectId + "~" + ewp.WorkPackageId;
                    if (oldRE != null)
                    {
                        _context.Notifications.Add(new Notification { UserId = oldRE, Message = "You have been removed from the work package " + ewp.WorkPackage!.Title + " in the project " + ewp.WorkPackage.Project!.ProjectTitle + " as a Responsible Engineer.", For = workPackageString + " Remove", Importance = 2 });
                    }
                    _context.Notifications.Add(new Notification { UserId = ewp.UserId, Message = "You have been added to the work package " + ewp.WorkPackage!.Title + " in the project " + ewp.WorkPackage.Project!.ProjectTitle + " as a Responsible Engineer.", For = workPackageString + " Add", Importance = 1 });
                    _context.SaveChanges();
                    return new JsonResult(user.FirstName + " " + user.LastName);
                }
            }
            return new JsonResult("Error!");
        }

        /// <summary>
        /// Creates and send the partial view which contains the work package creation form. Done this way to allow field validation.
        /// </summary>
        /// <returns>WP creation partial view</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> ShowSplitAsync(string workPackageId)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            string projectId = Convert.ToString(HttpContext.Session.GetInt32("CurrentProject") ?? 0);
            var projectBudget = _context.Budgets.Where(c => c.WPProjectId == projectId + "~" + workPackageId).ToList();
            if (projectBudget == null)
            {
                return BadRequest();
            }
            List<Budget> emptyBudgets = new List<Budget>();
            foreach (var item in _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year)!.ToList())
            {
                emptyBudgets.Add(new Budget
                {
                    LabourCode = item.LabourCode,
                    isREBudget = false,
                    Rate = item.Rate,
                    UnallocatedDays = projectBudget.Where(c => c.LabourCode == item.LabourCode).First().UnallocatedDays,
                    UnallocatedPeople = projectBudget.Where(c => c.LabourCode == item.LabourCode).First().UnallocatedPeople
                });
            }
            var model = new WorkPackageViewModel
            {
                budgets = emptyBudgets
            };
            return PartialView("_CreateWorkPackagePartial", model);
        }

        /// <summary>
        /// used to create a new WP from a parent WP.
        /// </summary>
        /// <param name="p">View model which contains new WP</param>
        /// <returns>the new WP, or the partial view with errors.</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> SplitAsync(WorkPackageViewModel p)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            //check if the fields are valid
            if (ModelState.GetFieldValidationState("WorkPackage.ParentWorkPackageId") != ModelValidationState.Valid || ModelState.GetFieldValidationState("WorkPackage.WorkPackageId") != ModelValidationState.Valid || ModelState.GetFieldValidationState("WorkPackage.Title") != ModelValidationState.Valid || ModelState.GetFieldValidationState("budgets") != ModelValidationState.Valid)
            {
                Response.StatusCode = 400;
                return PartialView("_CreateWorkPackagePartial", p);
            }
            if (p.WorkPackage.WorkPackageId.Length != 1)
            {
                ModelState.AddModelError("WorkPackage.WorkPackageId", "Work Package ID must be 1 character longer");
                Response.StatusCode = 400;
                return PartialView("_CreateWorkPackagePartial", p);
            }
            string newWPID = (p.WorkPackage!.ParentWorkPackageId == "0") ? p.WorkPackage.WorkPackageId : p.WorkPackage!.ParentWorkPackageId + p.WorkPackage!.WorkPackageId;
            if (p.WorkPackage != null && checkWorkPackage(newWPID))
            {
                ModelState.AddModelError("WorkPackage.WorkPackageId", "Work Package ID must be unique for this project");
                Response.StatusCode = 400;
                return PartialView("_CreateWorkPackagePartial", p);
            }
            CurrentProject = HttpContext.Session.GetInt32("CurrentProject");
            var parent = _context.WorkPackages!.FirstOrDefault(c => c.ProjectId == CurrentProject && c.WorkPackageId == p.WorkPackage!.ParentWorkPackageId);
            if (parent != null)
            {
                parent.IsBottomLevel = false;
                if (parent.IsClosed)
                {
                    return BadRequest();
                }
            }
            var newChild = new WorkPackage
            {
                WorkPackageId = newWPID,
                ProjectId = CurrentProject,
                ParentWorkPackageId = p.WorkPackage!.ParentWorkPackageId,
                ParentWorkPackageProjectId = CurrentProject ?? 0,
                IsBottomLevel = true,
                IsClosed = false,
                Title = p.WorkPackage.Title
            };
            //create budget for the new WP, one row per labour code.
            if (p.budgets != null)
            {
                Budget? parentB = null;
                List<Budget> parentBudgets = _context.Budgets.Where(c => c.WPProjectId == CurrentProject + "~" + newChild.ParentWorkPackageId).ToList();
                foreach (var budget in p.budgets)
                {
                    Budget newBudget = new Budget
                    {
                        WPProjectId = CurrentProject + "~" + newChild.WorkPackageId,
                        People = budget.People,
                        Days = budget.Days,
                        LabourCode = budget.LabourCode,
                        UnallocatedDays = budget.Days,
                        UnallocatedPeople = budget.People
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
            _context.WorkPackages!.Add(newChild);
            _context.SaveChanges();
            newChild.ParentWorkPackage = null;
            //create an empty RE if there isn't one, just to make it easier to display
            if (newChild.ResponsibleUser == null)
            {
                newChild.ResponsibleUser = new ApplicationUser
                {
                    FirstName = null,
                    LastName = null
                };
            }
            //find the total budget
            List<Budget> budgets = _context.Budgets.Where(c => c.WPProjectId == (newChild.ProjectId + "~" + newChild.WorkPackageId)).ToList();
            List<LabourGrade> lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
            double total = 0;
            double remaining = 0;
            foreach (var lg in lgs)
            {
                var budget = budgets.Where(c => c.WPProjectId == (newChild.ProjectId + "~" + newChild.WorkPackageId) && c.LabourCode == lg.LabourCode).First();
                total += budget.BudgetAmount * lg.Rate;
                remaining += budget.BudgetAmount * lg.Rate;
            }
            newChild.TotalBudget = total;
            newChild.TotalRemaining = remaining;
            newChild.Project = null;

            return Json(newChild);

        }

        /// <summary>
        /// Get the details of the budget for a work package.
        /// </summary>
        /// <param name="wp">Work package object that must contain the workpackageid</param>
        /// <returns></returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> BudgetDetailsAsync([FromBody] WorkPackage wp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            CurrentProject = HttpContext.Session.GetInt32("CurrentProject");
            var budgets = _context.Budgets.Where(c => c.WPProjectId == (CurrentProject + "~" + wp.WorkPackageId)).ToList();
            var lgs = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
            foreach (var budget in budgets)
            {
                budget.Rate = lgs.Where(c => c.LabourCode == budget.LabourCode).First().Rate;
            }
            List<List<Budget>> result = new List<List<Budget>>();
            result.Add(budgets.Where(c => c.isREBudget == false).ToList());
            result.Add(budgets.Where(c => c.isREBudget == true).ToList());
            return Json(result);
        }

        /// <summary>
        /// Used to close a work package. closing a work package will also close it's children if it has any.
        /// </summary>
        /// <param name="wp">the work package to close</param>
        /// <returns>200 if it worked, 400 if you made a bad request</returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> CloseWPAsync([FromBody] WorkPackage wp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;
            if (wp == null)
            {
                return BadRequest();
            }
            else
            {
                var closingwp = _context.WorkPackages
                    .Include(w => w.ChildWorkPackages)
                    .SingleOrDefault(w => w.WorkPackageId == wp.WorkPackageId && w.ProjectId == HttpContext.Session.GetInt32("CurrentProject"));
                if (closingwp != null)
                {
                    if (closingwp.ChildWorkPackages.Count != 0)
                    {
                        foreach (var child in closingwp.ChildWorkPackages)
                        {
                            await CloseWPAsync(child);
                        }
                    }
                    closingwp.IsClosed = true;
                }
                _context.SaveChanges();
                return Ok();
            }

        }

        /// <summary>
        /// For dealing with submitting the creation of a new project. Only HR or Admin may create a project.
        /// </summary>
        /// <param name="input">View model that contains the new project</param>
        /// <returns>Same page if errors, home page if not.</returns>
        [HttpPost]
        [Authorize(Roles = "HR,Admin", Policy = "KeyRequirement")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateProjectViewModel input)
        {
            if (_context.Projects.Find(input.project.ProjectId) != null)
            {
                ModelState.AddModelError("project.ProjectId", "Project ID must be unique.");
                Response.StatusCode = 400;
                ViewData["users"] = _context.Users.ToList(); ;
                return View(input);
            }
            if (ModelState.IsValid)
            {
                _context.Projects!.Add(input.project);
                _context.SaveChanges();

                //create a high level work package
                var newWP = new WorkPackage
                {
                    WorkPackageId = "0",
                    ProjectId = input.project.ProjectId,
                    IsBottomLevel = true,
                    Title = input.project.ProjectTitle
                };
                _context.WorkPackages!.Add(newWP);
                double totalBudget = 0;
                var grades = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year);

                //create the budget in the db, one row per labour code.
                if (input.budgets != null)
                {
                    foreach (var budget in input.budgets)
                    {
                        Budget newBudget = new Budget
                        {
                            WPProjectId = input.project.ProjectId + "~0",
                            People = budget.People,
                            Days = budget.Days,
                            LabourCode = budget.LabourCode,
                            UnallocatedDays = budget.Days,
                            UnallocatedPeople = budget.People
                        };
                        totalBudget += budget.BudgetAmount * grades.Where(c => budget.LabourCode == c.LabourCode).First().Rate;
                        _context.Budgets!.Add(newBudget);
                    }
                }
                input.project.TotalBudget = totalBudget;
                var projectString = input.project.ProjectId;
                _context.Notifications.Add(new Notification { UserId = input.project.ProjectManagerId, Message = "You have been added to the project " + input.project!.ProjectTitle + " as a Project Manager.", For = projectString + " Add", Importance = 1 });
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewData["users"] = _context.Users.ToList();
            return View(input);
        }

        /// <summary>
        /// Get all the employees that can be assigned to a work package. Also returns if they are currently already assigned.
        /// </summary>
        /// <param name="LowestLevelWp">The work package to get the employees for.</param>
        /// <returns>a json object of the available employees</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> GetWPEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;


            var userIdsInLLWP = await _context.EmployeeWorkPackages!
                .Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId && ewp.WorkPackageProjectId == HttpContext.Session.GetInt32("CurrentProject"))
                .Select(filtered => filtered.UserId)
                .ToListAsync();

            var budgets = await _context.Budgets
                .Where(c => c.WPProjectId == (HttpContext.Session.GetInt32("CurrentProject") + "~" + LowestLevelWp.WorkPackageId))
                .ToListAsync();

            var emp = await _context.EmployeeProjects!
                .Where(ep => ep.ProjectId == HttpContext.Session.GetInt32("CurrentProject"))
                .Select(e => new EmployeeWorkPackageViewModel
                {
                    Employee = e.User!,
                    Assigned = userIdsInLLWP.Contains(e.UserId)
                })
                .ToListAsync();

            foreach (var employee in emp)
            {
                var matchingBudget = budgets.FirstOrDefault(b => b.LabourCode == employee.Employee.LabourGradeCode);
                if (matchingBudget != null && employee.Assigned == true)
                {
                    matchingBudget.People--;
                }
            }

            var result = new List<object>();
            result.Add(emp.Select(e => new
            {
                e.Employee.Id,
                FirstName = e.Employee.FirstName ?? string.Empty,
                LastName = e.Employee.LastName ?? string.Empty,
                JobTitle = e.Employee.JobTitle ?? string.Empty,
                e.Assigned,
                LabourCode = e.Employee.LabourGradeCode ?? String.Empty
            }));
            result.Add(budgets.Select(c => new { c.LabourCode, c.People }));
            return new JsonResult(result);
        }


        /// <summary>
        /// Get the employees that are able to become Responsible Engineer.
        /// </summary>
        /// <param name="LowestLevelWp">The work package that needs an RE</param>
        /// <returns>json list of the employees</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> GetCandidateEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;
            var wp = _context.WorkPackages.Where(c => c.WorkPackageId == LowestLevelWp.WorkPackageId && c.ProjectId == HttpContext.Session.GetInt32("CurrentProject")).FirstOrDefault();
            if (wp == null)
            {
                return BadRequest();
            }
            var userIdsInLLWP = _context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId && ewp.WorkPackageProjectId == HttpContext.Session.GetInt32("CurrentProject")).Select(e => e.User);
            foreach (var user in userIdsInLLWP)
            {
                if (user != null && user.Id == wp.ResponsibleUserId)
                {
                    user.Selected = true;
                    break;
                }
            }
            return new JsonResult(userIdsInLLWP.Select(e => new { e!.Id, FirstName = e.FirstName!, LastName = e.LastName!, JobTitle = e.JobTitle!, e.Selected }));
        }

        /// <summary>
        /// Get the currently assigned employees for a work package
        /// </summary>
        /// <param name="LowestLevelWp">work package to get employees for</param>
        /// <returns>employees in work package</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> GetAssignedEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            return new JsonResult(_context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId && ewp.WorkPackageProjectId == HttpContext.Session.GetInt32("CurrentProject")).Select(e => e.User).Select(e => new { e!.Id, e.FirstName, e.LastName, e.JobTitle }));
        }


        /// <summary>
        /// Close a project, which also closes all of the work packages that the project has.
        /// </summary>
        /// <param name="id">id of the project to close</param>
        /// <returns>200 if it worked</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> CloseProject([FromBody] int id)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;
            if (id == 10)
            {
                return BadRequest();
            }
            var project = _context.Projects.Find(id);
            if (project == null)
            {
                return BadRequest();
            }
            project.IsClosed = true;
            foreach (var wp in _context.WorkPackages.Where(c => c.ProjectId == id).ToList())
            {
                wp.IsClosed = true;
            }
            _context.SaveChanges();
            return Ok();
        }

        /// <summary>
        /// Show an error if there is one
        /// </summary>
        /// <returns>error page</returns>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error");
        }
        /// <summary>
        /// used to check if a WP id is unique for this project
        /// </summary>
        /// <param name="WorkPackageId"></param>
        /// <returns></returns>
        private bool checkWorkPackage(string WorkPackageId)
        {
            int? project = HttpContext.Session.GetInt32("CurrentProject");
            var wp = _context.WorkPackages!.Where(c => c.ProjectId == project && c.WorkPackageId == WorkPackageId);
            if (wp != null && wp.Count() > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Generate the report that covers all the wps, up to the end of the last month. Has estimate to complete also.
        /// </summary>
        /// <param name="id">id of the project to create a report for</param>
        /// <returns>download of the report pdf</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Report(int id)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;
            MemoryStream ms = new MemoryStream();
            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdfDoc = new PdfDocument(writer);
            Document document = new Document(pdfDoc, PageSize.A4.Rotate(), false);
            writer.SetCloseStream(false);

            Paragraph header = new Paragraph("Project Cost Performace Report")
              .SetTextAlignment(TextAlignment.CENTER)
              .SetFontSize(15);
            document.Add(header);

            float fontSizeSH = 11.5F;
            Paragraph subheader = new Paragraph($"Created Date: {DateTime.Now.ToShortDateString()}").SetFontSize(fontSizeSH);
            document.Add(subheader);

            Project? prj = await _context.Projects!.FindAsync(id);
            if (prj == null)
            {
                return BadRequest();
            }
            ApplicationUser? mgr = await _context.Users.FindAsync(prj!.ProjectManagerId);
            if (mgr == null)
            {
                return BadRequest();
            }

            var startOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var firstDay = DateOnly.FromDateTime(startOfThisMonth.AddMonths(-1));
            var lastDay = DateOnly.FromDateTime(startOfThisMonth.AddDays(-1));


            LineSeparator ls = new LineSeparator(new SolidLine());

            Paragraph details = new Paragraph();
            details.Add(new Text($"Project Title: {prj.ProjectTitle}"));
            details.Add(new Tab());
            details.Add(new Tab());
            details.Add(new Text($"Manager: {mgr.FirstName} {mgr.LastName} ({mgr.EmployeeNumber})"));
            details.SetFontSize(fontSizeSH);
            document.Add(details);

            DateTime previousFriday = startOfThisMonth.AddDays(-1).AddDays(-(int)startOfThisMonth.AddDays(-1).DayOfWeek - 2);


            Paragraph dates = new Paragraph();
            dates.Add(new Text($"End Date: {previousFriday.ToShortDateString()}"));
            dates.SetFontSize(fontSizeSH);
            document.Add(dates);
            document.Add(ls);

            Table wpTable = new Table(9);
            //headings row

            wpTable.AddCell(new Cell()
               .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
               .SetTextAlignment(TextAlignment.CENTER)
               .SetFontSize(fontSizeSH)
               .Add(new Paragraph("Work Package")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Engineers")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Stats")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Project Budget")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Engineer Planned")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Actual to date")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Estimate at Completion")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("% Variance")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("% Complete")));

            wpTable.SetWidth(UnitValue.CreatePercentValue(100));

            //get all labour grades and budgets in one query so there aren't lots of little ones.
            var labourGrades = _context.LabourGrades.ToList();
            var budgets = _context.Budgets.Where(c => c.WPProjectId.StartsWith(prj.ProjectId + "~")).ToList();
            var estimates = _context.ResponsibleEngineerEstimates.Where(c => c.WPProjectId!.StartsWith(prj.ProjectId + "~")).ToList();

            //get all the approved timesheets of users in a project in the last month.
            var employees = _context.EmployeeProjects.Where(c => c.ProjectId == prj.ProjectId).Select(c => c.UserId).ToList();
            var eWps = _context.EmployeeWorkPackages.Where(c => c.WorkPackageProjectId == prj.ProjectId).Include(c => c.User).ToList();
            var timesheets = _context.Timesheets
                .Where(c => c.TimesheetApproverId != null && c.EndDate <= lastDay && employees.Contains(c.UserId))
                .Include(c => c.TimesheetApprover)
                .Include(c => c.TimesheetRows);            //create a list of timesheet rows after verifying timesheets
            var timesheetRows = new List<TimesheetRow>();
            TimesheetController tc = new TimesheetController(_context, _userManager);
            foreach (var timesheet in timesheets)
            {
                //check if timesheet is legit
                if (tc.verifySignature(timesheet, timesheet.TimesheetApprover!.PublicKey!, timesheet.ApproverHash!))
                {
                    timesheetRows.AddRange(timesheet.TimesheetRows.Where(c => c.ProjectId == prj.ProjectId).ToList());
                }
            }

            foreach (var wp in _context.WorkPackages.Where(c => c.ProjectId == prj.ProjectId).OrderBy(c => c.WorkPackageId).ToList())
            {
                //wp info
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(wp.WorkPackageId).SetFontSize(fontSizeSH))
                    .Add(new Paragraph(wp.Title).SetFontSize(fontSizeSH)));

                Cell engineers = new Cell();
                foreach (var employee in eWps.Where(c => c.WorkPackageId == wp.WorkPackageId).Select(c => c.User))
                {
                    if (employee != null)
                    {
                        engineers.Add(new Paragraph(employee.FirstName + " " + employee.LastName![0]));
                    }
                }
                wpTable.AddCell(engineers);

                //label column
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph("Total P.D.").SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.RIGHT))
                    .Add(new Paragraph("Labour $").SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.RIGHT)));

                //PM budget
                double totalPDPM = 0;
                double totalCostPM = 0;
                foreach (var budget in budgets.Where(c => c.WPProjectId == prj.ProjectId + "~" + wp.WorkPackageId && c.isREBudget == false))
                {
                    totalPDPM += budget.BudgetAmount;
                    totalCostPM += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
                }
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(Math.Round(totalPDPM, 2))).SetFontSize(fontSizeSH))
                    .Add(new Paragraph("$" + Math.Round(totalCostPM, 2)).SetFontSize(fontSizeSH)));

                //RE budget
                double totalPDRE = 0;
                double totalCostRE = 0;
                foreach (var budget in budgets.Where(c => c.WPProjectId == prj.ProjectId + "~" + wp.WorkPackageId && c.isREBudget == true))
                {
                    totalPDRE += budget.BudgetAmount;
                    totalCostRE += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
                }
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(Math.Round(totalPDRE, 2))).SetFontSize(fontSizeSH))
                    .Add(new Paragraph("$" + Math.Round(totalCostRE, 2)).SetFontSize(fontSizeSH)));


                double totalPDActual = 0;
                double totalCostActual = 0;
                foreach (var row in timesheetRows.Where(c => c.WorkPackageId == wp.WorkPackageId))
                {
                    totalPDActual = totalPDActual + (row.TotalHoursRow / 8);
                    totalCostActual += (row.TotalHoursRow / 8) * labourGrades.Where(c => c.LabourCode == row.OriginalLabourCode && c.Year == row.Timesheet!.EndDate!.Value.Year).First().Rate;
                }
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(Math.Round(totalPDActual, 2))).SetFontSize(fontSizeSH))
                    .Add(new Paragraph("$" + Math.Round(totalCostActual, 2)).SetFontSize(fontSizeSH)));


                double pDEstimate = totalPDActual;
                double costEstimate = totalCostActual;
                foreach (var estimate in estimates.Where(c => c.WPProjectId == prj.ProjectId + "~" + wp.WorkPackageId).ToList())
                {
                    pDEstimate += estimate.EstimatedCost;
                    costEstimate += estimate.EstimatedCost * labourGrades.Where(c => c.LabourCode == estimate.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
                }
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(Math.Round(pDEstimate, 2))).SetFontSize(fontSizeSH))
                    .Add(new Paragraph("$" + Math.Round(costEstimate, 2)).SetFontSize(fontSizeSH)));


                //find the percent variance
                double pdVariance = (pDEstimate - totalPDPM) / Math.Max(totalPDPM, pDEstimate) * 100;
                double costVariance = (costEstimate - totalCostPM) / Math.Max(totalCostPM, costEstimate) * 100;

                pdVariance = double.IsNaN(pdVariance) ? 0 : pdVariance;
                costVariance = double.IsNaN(costVariance) ? 0 : costVariance;

                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(Math.Round(pdVariance))).SetFontSize(fontSizeSH))
                    .Add(new Paragraph(Convert.ToString(Math.Round(costVariance))).SetFontSize(fontSizeSH)));

                double percentComplete = totalCostActual / costEstimate * 100;
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(double.IsNaN(percentComplete) ? 0 : Math.Round(percentComplete))).SetFontSize(fontSizeSH)));

            }

            document.Add(wpTable);


            // Page Numbers
            int n = pdfDoc.GetNumberOfPages();
            for (int i = 1; i <= n; i++)
            {
                document.ShowTextAligned(new Paragraph(String
                  .Format("Page " + i + " of " + n)),
                  559, 806, i, TextAlignment.RIGHT,
                  VerticalAlignment.TOP, 0);
            }

            document.Close();
            byte[] byteInfo = ms.ToArray();
            ms.Write(byteInfo, 0, byteInfo.Length);
            ms.Position = 0;

            FileStreamResult fileStreamResult = new FileStreamResult(ms, "application/pdf");
            //Uncomment this to return the file as a download
            fileStreamResult.FileDownloadName = "Report-" + prj.ProjectId + "-" + DateTime.Now.ToShortDateString() + ".pdf";
            return fileStreamResult;
        }

        /// <summary>
        /// generate a report of everything that happened last week.
        /// </summary>
        /// <param name="id">id of the project to get a report for</param>
        /// <returns>pdf download of the report</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> WeekReport(int id)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;
            MemoryStream ms = new MemoryStream();
            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdfDoc = new PdfDocument(writer);
            Document document = new Document(pdfDoc, PageSize.A4.Rotate(), false);
            writer.SetCloseStream(false);

            Paragraph header = new Paragraph("Week Details Report")
              .SetTextAlignment(TextAlignment.CENTER)
              .SetFontSize(15);
            document.Add(header);

            float fontSizeSH = 11.5F;
            Paragraph subheader = new Paragraph($"Created Date: {DateTime.Now.ToShortDateString()}").SetFontSize(fontSizeSH);
            document.Add(subheader);

            Project? prj = await _context.Projects!.FindAsync(id);
            if (prj == null)
            {
                return BadRequest();
            }
            ApplicationUser? mgr = await _context.Users.FindAsync(prj!.ProjectManagerId);
            if (mgr == null)
            {
                return BadRequest();
            }

            DateTime today = DateTime.Today;
            int daysUntilFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
            DateTime lastDay = today.AddDays(daysUntilFriday - 7);
            DateTime firstDay = lastDay.AddDays(-6);


            LineSeparator ls = new LineSeparator(new SolidLine());

            Paragraph details = new Paragraph();
            details.Add(new Text($"Project Title: {prj.ProjectTitle}"));
            details.Add(new Tab());
            details.Add(new Tab());
            details.Add(new Text($"Manager: {mgr.FirstName} {mgr.LastName} ({mgr.EmployeeNumber})"));
            details.SetFontSize(fontSizeSH);
            document.Add(details);

            Paragraph dates = new Paragraph();
            dates.Add(new Text($"Start Date: {firstDay.ToShortDateString()}"));
            dates.Add(new Tab());
            dates.Add(new Tab());
            dates.Add(new Text($"End Date: {lastDay.ToShortDateString()}"));
            dates.SetFontSize(fontSizeSH);
            document.Add(dates);
            document.Add(ls);

            Table wpTable = new Table(18);
            //headings row

            wpTable.AddCell(new Cell()
               .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
               .SetTextAlignment(TextAlignment.CENTER)
               .SetFontSize(fontSizeSH)
               .Add(new Paragraph("Work Package")));

            wpTable.AddCell(new Cell()
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Employees")));

            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Sat")));

            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Sun")));
            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Mon")));
            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Tue")));
            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Wed")));
            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Thu")));
            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Fri")));
            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Total")));

            wpTable.AddCell(new Cell(1, 2)
               .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
               .SetTextAlignment(TextAlignment.CENTER)
               .SetFontSize(fontSizeSH)
               .Add(new Paragraph("")));

            for (int i = 0; i < 8; i++)
            {
                wpTable.AddCell(new Cell()
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(fontSizeSH)
                    .Add(new Paragraph("Hour")));

                wpTable.AddCell(new Cell()
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(fontSizeSH)
                    .Add(new Paragraph("$")));
            }

            wpTable.SetWidth(UnitValue.CreatePercentValue(100));

            //get all labour grades and budgets in one query so there aren't lots of little ones.
            var labourGrades = _context.LabourGrades.ToList();
            //get all the approved timesheets of users in a project in the last month.
            var employees = _context.EmployeeProjects.Where(c => c.ProjectId == prj.ProjectId).Select(c => c.UserId).ToList();
            var eWps = _context.EmployeeWorkPackages.Where(c => c.WorkPackageProjectId == prj.ProjectId).Include(c => c.User).ToList();
            var timesheets = _context.Timesheets
                .Where(c => c.TimesheetApproverId != null && c.EndDate == DateOnly.FromDateTime(lastDay) && employees.Contains(c.UserId))
                .Include(c => c.TimesheetApprover)
                .Include(c => c.TimesheetRows);            //create a list of timesheet rows after verifying timesheets
            var timesheetRows = new List<TimesheetRow>();
            TimesheetController tc = new TimesheetController(_context, _userManager);
            foreach (var timesheet in timesheets)
            {
                //check if timesheet is legit
                if (tc.verifySignature(timesheet, timesheet.TimesheetApprover!.PublicKey!, timesheet.ApproverHash!))
                {
                    timesheetRows.AddRange(timesheet.TimesheetRows.Where(c => c.ProjectId == prj.ProjectId).ToList());
                }
            }
            double[] dayTotals = { 0, 0, 0, 0, 0, 0, 0 };
            double[] dayTotalsMoney = { 0, 0, 0, 0, 0, 0, 0 };
            double grandTotal = 0;
            double grandTotalMoney = 0;
            foreach (var wp in _context.WorkPackages.Where(c => c.ProjectId == prj.ProjectId).Include(c => c.EmployeeWorkPackages!).ThenInclude(c => c.User).OrderBy(c => c.WorkPackageId).ToList())
            {
                if (wp.EmployeeWorkPackages != null && wp.EmployeeWorkPackages.Count() > 0)
                {
                    //wp info
                    wpTable.AddCell(new Cell(wp.EmployeeWorkPackages.Count(), 1)
                        .Add(new Paragraph(wp.WorkPackageId).SetFontSize(fontSizeSH))
                        .Add(new Paragraph(wp.Title).SetFontSize(fontSizeSH)));

                    foreach (var ewp in wp.EmployeeWorkPackages)
                    {
                        var user = ewp.User;
                        wpTable.AddCell(new Cell()
                            .Add(new Paragraph(user!.FirstName + " " + user.LastName).SetFontSize(fontSizeSH)));

                        double totalMoney = 0;
                        double totalHour = 0;
                        var row = timesheetRows.Where(c => c.WorkPackageId == ewp.WorkPackageId && c.Timesheet!.UserId == user.Id).FirstOrDefault();
                        for (int i = 0; i < 7; i++)
                        {
                            double hour = 0;
                            double money = 0;
                            if (row != null)
                            {
                                hour = row.getHour(i);
                                totalHour += hour;
                                grandTotal += hour;
                                dayTotals[i] += hour;
                                money = hour * labourGrades.Where(c => c.Year == DateTime.Now.Year && c.LabourCode == row.OriginalLabourCode).First().Rate / 8;
                                totalMoney += money;
                                dayTotalsMoney[i] += money;
                                grandTotalMoney += money;
                            }

                            wpTable.AddCell(new Cell()
                                .Add(new Paragraph(Convert.ToString(Math.Round(hour, 2))).SetFontSize(fontSizeSH)));
                            wpTable.AddCell(new Cell()
                                .Add(new Paragraph(Convert.ToString(Math.Round(money, 2))).SetFontSize(fontSizeSH)));
                        }
                        wpTable.AddCell(new Cell()
                            .Add(new Paragraph(Convert.ToString(Math.Round(totalHour, 2))).SetFontSize(fontSizeSH)));
                        wpTable.AddCell(new Cell()
                            .Add(new Paragraph(Convert.ToString(Math.Round(totalMoney, 2))).SetFontSize(fontSizeSH)));
                    }
                }

            }
            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.RIGHT)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Total")));
            for (int i = 0; i < 7; i++)
            {
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(Math.Round(dayTotals[i], 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                wpTable.AddCell(new Cell()
                    .Add(new Paragraph(Convert.ToString(Math.Round(dayTotalsMoney[i], 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
            }
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(Math.Round(grandTotal, 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(Math.Round(grandTotalMoney, 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
            document.Add(wpTable);


            // Page Numbers
            int n = pdfDoc.GetNumberOfPages();
            for (int i = 1; i <= n; i++)
            {
                document.ShowTextAligned(new Paragraph(String
                  .Format("Page " + i + " of " + n)),
                  559, 806, i, TextAlignment.RIGHT,
                  VerticalAlignment.TOP, 0);
            }

            document.Close();
            byte[] byteInfo = ms.ToArray();
            ms.Write(byteInfo, 0, byteInfo.Length);
            ms.Position = 0;

            FileStreamResult fileStreamResult = new FileStreamResult(ms, "application/pdf");
            //Uncomment this to return the file as a download
            fileStreamResult.FileDownloadName = "Report-" + prj.ProjectId + "-" + DateTime.Now.ToShortDateString() + ".pdf";
            return fileStreamResult;
        }

        /// <summary>
        /// Get a list of all the employees, in order to chooose an assistant pm
        /// </summary>
        /// <returns></returns>
        [Authorize(Policy = "KeyRequirement")]
        [HttpGet]
        public async Task<IActionResult> GetAllEmployees()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = await _userManager.GetUserIdAsync(user);
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            var employees = await _context.EmployeeProjects
                .Where(c => c.ProjectId == projectId && c.UserId != userId)
                .Include(c => c.Project)
                    .ThenInclude(p => p!.AssistantProjectManager)
                .Include(c => c.Project!.ProjectManager)
                .Include(c => c.User)
                .Select(c => new
                {
                    c!.User!.FirstName,
                    c!.User!.LastName,
                    c!.User!.EmployeeNumber,
                    ManagerNumber = c.Project != null && c.Project.AssistantProjectManager != null ? c.Project.AssistantProjectManager.EmployeeNumber : 0,
                    ProjectManagerNumber = c.Project != null && c.Project.ProjectManager != null ? c.Project.ProjectManager.EmployeeNumber : 0
                })
                .ToListAsync();



            return Json(employees);
        }

        /// <summary>
        /// Assign the assistant PM
        /// </summary>
        /// <param name="asm">Assistant PM id</param>
        /// <returns>200 if its good</returns>
        [Authorize(Policy = "KeyRequirement")]
        [HttpPost]
        public async Task<IActionResult> AssignASM([FromBody] String? asm)
        {
            int? asmId = Convert.ToInt32(asm);
            if (asmId == null)
            {
                return BadRequest();
            }
            int projectId = HttpContext.Session.GetInt32("CurrentProject") ?? 0;
            var proj = await _context.Projects.FindAsync(projectId);
            if (proj != null)
            {
                var user = _context.Users.Where(c => c.EmployeeNumber == asmId).Select(c => c.Id).First();
                if (user == proj.ProjectManagerId)
                {
                    return BadRequest();
                }
                var oldASM = proj.AssistantProjectManagerId;
                proj.AssistantProjectManagerId = user;
                var projectString = projectId;
                if (oldASM != null)
                {
                    _context.Notifications.Add(new Notification { UserId = oldASM, Message = "You have been removed from the project " + proj!.ProjectTitle + " as an Assistant Project Manager.", For = projectString + " Remove", Importance = 2 });
                }
                _context.Notifications.Add(new Notification { UserId = proj.AssistantProjectManagerId, Message = "You have been added to the project " + proj!.ProjectTitle + " as an Assistant Project Manager.", For = projectString + " Add", Importance = 1 });
                _context.SaveChanges();
                return Ok();
            }
            return new JsonResult("Error!");
        }

        /// <summary>
        /// Generate a PCBAC report to see the original budget vs the RE budget vs the cost to date
        /// </summary>
        /// <param name="id">id of the project you need a budget for</param>
        /// <returns>a pdf download of the report</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> PCBAC(int id)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;
            MemoryStream ms = new MemoryStream();
            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdfDoc = new PdfDocument(writer);
            Document document = new Document(pdfDoc, PageSize.A4.Rotate(), false);
            writer.SetCloseStream(false);

            Paragraph header = new Paragraph("Project Costing/Budget/Actual Comparison (PCBAC)")
              .SetTextAlignment(TextAlignment.CENTER)
              .SetFontSize(15);
            document.Add(header);

            float fontSizeSH = 11.5F;
            Paragraph subheader = new Paragraph($"Created Date: {DateTime.Now.ToShortDateString()}").SetFontSize(fontSizeSH);
            document.Add(subheader);

            Project? prj = await _context.Projects!.FindAsync(id);
            if (prj == null)
            {
                return BadRequest();
            }
            ApplicationUser? mgr = await _context.Users.FindAsync(prj!.ProjectManagerId);
            if (mgr == null)
            {
                return BadRequest();
            }

            LineSeparator ls = new LineSeparator(new SolidLine());

            Paragraph details = new Paragraph();
            details.Add(new Text($"Project Title: {prj.ProjectTitle}"));
            details.Add(new Tab());
            details.Add(new Tab());
            details.Add(new Text($"Manager: {mgr.FirstName} {mgr.LastName} ({mgr.EmployeeNumber})"));
            details.SetFontSize(fontSizeSH);
            document.Add(details);


            document.Add(ls);

            Table wpTable = new Table(8);
            //headings row

            wpTable.AddCell(new Cell(2, 2)
               .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
               .SetVerticalAlignment(VerticalAlignment.MIDDLE)
               .SetTextAlignment(TextAlignment.CENTER)
               .SetFontSize(fontSizeSH)
               .Add(new Paragraph("Labour")));

            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Project Manager's Budget")));

            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Responsible Engineer's Budget")));

            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Actual Cost To Date")));

            for (int i = 0; i < 3; i++)
            {
                wpTable.AddCell(new Cell()
                   .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetFontSize(fontSizeSH)
                   .Add(new Paragraph("PD")));

                wpTable.AddCell(new Cell()
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(fontSizeSH)
                    .Add(new Paragraph("$")));
            }

            wpTable.SetWidth(UnitValue.CreatePercentValue(100));

            //get all labour grades and budgets in one query so there aren't lots of little ones.
            var labourGrades = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
            var budgets = _context.Budgets.Where(c => c.WPProjectId.StartsWith(prj.ProjectId + "~")).ToList();
            var estimates = _context.ResponsibleEngineerEstimates.Where(c => c.WPProjectId!.StartsWith(prj.ProjectId + "~")).ToList();

            //get all the approved timesheets of users in a project in the last month.
            var employees = _context.EmployeeProjects.Where(c => c.ProjectId == prj.ProjectId).Select(c => c.UserId).ToList();
            var eWps = _context.EmployeeWorkPackages.Where(c => c.WorkPackageProjectId == prj.ProjectId).Include(c => c.User).ToList();
            var timesheets = _context.Timesheets
                .Where(c => c.TimesheetApproverId != null && employees.Contains(c.UserId))
                .Include(c => c.TimesheetApprover)
                .Include(c => c.TimesheetRows);            //create a list of timesheet rows after verifying timesheets
            var timesheetRows = new List<TimesheetRow>();
            TimesheetController tc = new TimesheetController(_context, _userManager);
            foreach (var timesheet in timesheets)
            {
                //check if timesheet is legit
                if (tc.verifySignature(timesheet, timesheet.TimesheetApprover!.PublicKey!, timesheet.ApproverHash!))
                {
                    timesheetRows.AddRange(timesheet.TimesheetRows.Where(c => c.ProjectId == prj.ProjectId).ToList());
                }
            }

            double totalPM = 0;
            double totalPMPD = 0;
            double totalRE = 0;
            double totalREPD = 0;
            double totalActualPD = 0;
            double totalActual = 0;
            foreach (var lg in labourGrades)
            {
                wpTable.AddCell(new Cell(1, 2).Add(new Paragraph(lg.LabourCode + " ($" + lg.Rate + ")")));


                double totalPDPM = 0;
                double totalCostPM = 0;
                foreach (var budget in budgets.Where(c => c.isREBudget == false && c.LabourCode == lg.LabourCode))
                {
                    totalPDPM += budget.BudgetAmount;
                    totalCostPM += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
                }
                totalPM += totalCostPM;
                totalPMPD += totalPDPM;
                wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalPDPM, 2))).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
                wpTable.AddCell(new Cell().Add(new Paragraph("$" + Math.Round(totalCostPM, 2)).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));



                double totalPDRE = 0;
                double totalCostRE = 0;
                foreach (var budget in budgets.Where(c => c.isREBudget == true && c.LabourCode == lg.LabourCode))
                {
                    totalPDRE += budget.BudgetAmount;
                    totalCostRE += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
                }
                totalRE += totalCostRE;
                totalREPD += totalPDRE;
                wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalPDRE, 2))).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
                wpTable.AddCell(new Cell().Add(new Paragraph("$" + Math.Round(totalCostRE, 2)).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));


                double totalPDActual = 0;
                double totalCostActual = 0;
                foreach (var row in timesheetRows.Where(c => c.OriginalLabourCode == lg.LabourCode))
                {
                    totalPDActual += row.TotalHoursRow / 8;
                    totalCostActual += (row.TotalHoursRow / 8) * labourGrades.Where(c => c.LabourCode == row.OriginalLabourCode && c.Year == row.Timesheet!.EndDate!.Value.Year).First().Rate;
                }
                totalActualPD += totalPDActual;
                totalActual += totalCostActual;
                wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalPDActual, 2))).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
                wpTable.AddCell(new Cell().Add(new Paragraph("$" + Math.Round(totalCostActual, 2)).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
            }

            wpTable.AddCell(new Cell(1, 2)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .Add(new Paragraph("Total")));

            wpTable.AddCell(new Cell()
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .Add(new Paragraph(Convert.ToString(Math.Round(totalPMPD, 2)))));

            wpTable.AddCell(new Cell()
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .Add(new Paragraph("$" + Convert.ToString(Math.Round(totalPM, 2)))));

            wpTable.AddCell(new Cell()
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .Add(new Paragraph(Convert.ToString(Math.Round(totalREPD, 2)))));

            wpTable.AddCell(new Cell()
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .Add(new Paragraph("$" + Convert.ToString(Math.Round(totalRE, 2)))));

            wpTable.AddCell(new Cell()
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .Add(new Paragraph(Convert.ToString(Math.Round(totalActualPD, 2)))));

            wpTable.AddCell(new Cell()
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(fontSizeSH)
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .Add(new Paragraph("$" + Convert.ToString(Math.Round(totalActual, 2)))));


            document.Add(wpTable);


            // Page Numbers
            int n = pdfDoc.GetNumberOfPages();
            for (int i = 1; i <= n; i++)
            {
                document.ShowTextAligned(new Paragraph(String
                  .Format("Page " + i + " of " + n)),
                  559, 806, i, TextAlignment.RIGHT,
                  VerticalAlignment.TOP, 0);
            }

            document.Close();
            byte[] byteInfo = ms.ToArray();
            ms.Write(byteInfo, 0, byteInfo.Length);
            ms.Position = 0;

            FileStreamResult fileStreamResult = new FileStreamResult(ms, "application/pdf");
            //Uncomment this to return the file as a download
            fileStreamResult.FileDownloadName = "Report-" + prj.ProjectId + "-" + DateTime.Now.ToShortDateString() + ".pdf";
            return fileStreamResult;
        }


        /// <summary>
        /// can be used to make sure the user is the pm or assistant pm for the project.
        /// </summary>
        /// <returns></returns>
        private async Task<IActionResult?> verifyPMAsync()
        {
            CurrentProject = HttpContext.Session.GetInt32("CurrentProject");
            ApplicationUser user = (await _userManager.GetUserAsync(User))!;
            if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "HR"))
            {
                return null;
            }

            var project = _context.Projects.First(c => c.ProjectId == CurrentProject);
            if (user.Id != project.ProjectManagerId && user.Id != project.AssistantProjectManagerId)
            {
                return Challenge();
            }
            return null;
        }

        /// <summary>
        /// Get the current user to see if the assign asm box should be hidden.
        /// </summary>
        /// <returns>the id of the current user</returns>
        [Authorize(Policy = "KeyRequirement")]
        [HttpGet]
        public async Task<IActionResult> FindPM()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = await _userManager.GetUserIdAsync(user);
            return Json(userId);
        }
    }
}