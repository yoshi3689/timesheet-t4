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

namespace TimesheetApp.Controllers
{
    /// <summary>
    /// Deals with creating and managing projects, which includes creating and updating work packages.
    /// </summary>
    [Authorize]
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
        public IActionResult Index()
        {
            if (User.Identity!.IsAuthenticated && (User.IsInRole("HR") || User.IsInRole("Admin")))
            {
                var projects = _context.Projects!.Include(s => s.ProjectManager);
                return View(projects);
            }
            else
            {
                var userId = _userManager.GetUserId(HttpContext.User);
                var project = _context.Projects!.Where(s => s.ProjectManager!.Id == userId || s.AssistantProjectManagerId == userId).Include(s => s.ProjectManager);
                return View(project);
            }
        }

        /// <summary>
        /// Gets the page for creating a new project. Only HR or admin may create a project.
        /// </summary>
        /// <returns>new project page.</returns>
        [Authorize(Roles = "HR,Admin")]
        public IActionResult Create()
        {
            var users = _context.Users.Select(s => new
            {
                Id = s.Id,
                Name = s.FirstName + " " + s.LastName
            });
            ViewData["UserId"] = new SelectList(users, "Id", "Name");
            CreateProjectViewModel proj = new CreateProjectViewModel
            {
                budgets = _context.LabourGrades.Select(lg => new Budget
                {
                    LabourCode = lg.LabourCode,
                    LabourGrade = lg,
                    isREBudget = false,
                    Rate = lg.Rate,
                }).ToList()
            };
            return View(proj);
        }

        /// <summary>
        /// For dealing with submitting the creation of a new project. Only HR or Admin may create a project.
        /// </summary>
        /// <param name="input">View model that contains the new project</param>
        /// <returns>Same page if errors, home page if not.</returns>
        [HttpPost]
        [Authorize(Roles = "HR,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateProjectViewModel input)
        {
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
                var grades = _context.LabourGrades;

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
                            Remaining = budget.BudgetAmount
                        };
                        totalBudget += budget.BudgetAmount * grades.Where(c => budget.LabourCode == c.LabourCode).First().Rate;
                        _context.Budgets!.Add(newBudget);
                    }
                }
                input.project.TotalBudget = totalBudget;
                _context.SaveChanges();
                return RedirectToAction("Index");

            }
            var users = _context.Users.Select(s => new
            {
                Id = s.Id,
                Name = s.FirstName + " " + s.LastName
            });
            ViewData["UserId"] = new SelectList(users, "Id", "Name");
            return View(input);
        }

        /// <summary>
        /// Returns the page which is used to manage the project, including creation of work packages.
        /// </summary>
        /// <param name="id">the id of the project</param>
        /// <returns>The project manage page</returns>
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
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
                LabourGrades = _context.LabourGrades.ToList()
            };

            //calculate the total budget amount for each work package.
            List<Budget> budgets = _context.Budgets.Where(c => c.WPProjectId.StartsWith(CurrentProject + "~")).ToList();
            model.wps = getTotalMoney(model.wps, budgets);
            return View(model);
        }

        private List<WorkPackage> getTotalMoney(List<WorkPackage> wps, List<Budget> budgets)
        {
            List<LabourGrade> lgs = _context.LabourGrades.ToList();
            foreach (var wp in wps)
            {
                double total = 0;
                double remaining = 0;
                foreach (var lg in lgs)
                {
                    var budget = budgets.Where(c => c.WPProjectId == (wp.ProjectId + "~" + wp.WorkPackageId) && c.LabourCode == lg.LabourCode).First();
                    total += budget.BudgetAmount * lg.Rate;
                    remaining += budget.Remaining * lg.Rate;
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
        [Authorize]
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

            var removedEmployeeIds = _context.EmployeeWorkPackages.Where(c => c.WorkPackageId == workPackageId && c.WorkPackageProjectId == workPackageProjectId).Select(c => c.UserId).ToList();
            _context.EmployeeWorkPackages.RemoveRange(_context.EmployeeWorkPackages.Where(c => c.WorkPackageId == workPackageId && c.WorkPackageProjectId == workPackageProjectId));

            var addedEmployeeIds = ewps.Where(e => e.UserId != null).Select(e => e.UserId).ToList();

            var notifiedAddedEmployeeIds = addedEmployeeIds.Except(removedEmployeeIds).ToList();
            var notifiedRemovedEmployeeIds = removedEmployeeIds.Except(addedEmployeeIds).ToList();

            _context.EmployeeWorkPackages.AddRange(ewps.Where(e => e.UserId != null));

            var workPackageString = workPackageProjectId + "~" + workPackageId;

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
        [Authorize]
        public async Task<IActionResult> AssignResponsibleEngineerAsync([FromBody] EmployeeWorkPackage ewp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            if (ewp != null)
            {
                var LLWP = await _context.WorkPackages.FindAsync(ewp.WorkPackageId, ewp.WorkPackageProjectId);
                if (LLWP != null)
                {
                    LLWP.ResponsibleUserId = ewp.UserId;

                    // add rows of estimate for this LLWP
                    var user = _context.Users.Where(c => c.Id == ewp.UserId).First();
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
        public async Task<IActionResult> ShowSplitAsync()
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            string projectId = Convert.ToString(HttpContext.Session.GetInt32("CurrentProject") ?? 0);
            var projectBudget = _context.Budgets.Where(c => c.WPProjectId == projectId + "~0").ToList();
            List<Budget> emptyBudgets = new List<Budget>();
            foreach (var item in _context.LabourGrades!.ToList())
            {
                emptyBudgets.Add(new Budget
                {
                    LabourCode = item.LabourCode,
                    LabourGrade = item,
                    isREBudget = false,
                    Rate = item.Rate,
                    Remaining = projectBudget.Where(c => c.LabourCode == item.LabourCode).First().Remaining
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
        [Authorize]
        public async Task<IActionResult> SplitAsync(WorkPackageViewModel p)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            //check if the fields are valid
            if (ModelState.GetFieldValidationState("WorkPackage.ParentWorkPackageId") != ModelValidationState.Valid || ModelState.GetFieldValidationState("WorkPackage.WorkPackageId") != ModelValidationState.Valid || ModelState.GetFieldValidationState("WorkPackage.Title") != ModelValidationState.Valid)
            {
                Response.StatusCode = 400;
                return PartialView("_CreateWorkPackagePartial", p);
            }
            if (p.WorkPackage != null && checkWorkPackage(p.WorkPackage.WorkPackageId))
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
            }
            string newWPID = (p.WorkPackage!.ParentWorkPackageId == "0") ? p.WorkPackage.WorkPackageId : p.WorkPackage!.ParentWorkPackageId + p.WorkPackage!.WorkPackageId;
            var newChild = new WorkPackage
            {
                WorkPackageId = newWPID,
                ProjectId = CurrentProject,
                ParentWorkPackageId = p.WorkPackage.ParentWorkPackageId,
                ParentWorkPackageProjectId = CurrentProject ?? 0,
                IsBottomLevel = true,
                IsClosed = false,
                Title = p.WorkPackage.Title
            };
            //create budget for the new WP, one row per labour code.
            if (p.budgets != null)
            {
                List<Budget> parentBudgets = _context.Budgets.Where(c => c.WPProjectId == CurrentProject + "~" + newChild.ParentWorkPackageId).ToList();
                foreach (var budget in p.budgets)
                {
                    Budget newBudget = new Budget
                    {
                        WPProjectId = CurrentProject + "~" + newChild.WorkPackageId,
                        People = budget.People,
                        Days = budget.Days,
                        LabourCode = budget.LabourCode,
                        Remaining = budget.BudgetAmount
                    };
                    _context.Budgets!.Add(newBudget);
                    parentBudgets.Where(c => c.LabourCode == budget.LabourCode).First().Remaining -= newBudget.BudgetAmount;
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
            List<LabourGrade> lgs = _context.LabourGrades.ToList();
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
        [Authorize]
        public async Task<IActionResult> BudgetDetailsAsync([FromBody] WorkPackage wp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            CurrentProject = HttpContext.Session.GetInt32("CurrentProject");
            var budgets = _context.Budgets.Where(c => c.WPProjectId == (CurrentProject + "~" + wp.WorkPackageId)).ToList();
            var lgs = _context.LabourGrades.ToList();
            foreach (var budget in budgets)
            {
                budget.LabourGrade = null;
                budget.Rate = lgs.Where(c => c.LabourCode == budget.LabourCode).First().Rate;
            }
            return Json(budgets);
        }

        //get employees for a wp, and say if they are already assigned or not.
        [Authorize]
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
                if (matchingBudget != null)
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


        // get employees assigned to this lowest wpkg who are not a reponsible eng of this wpkg
        [Authorize]
        public async Task<IActionResult> GetCandidateEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            // get empIds assigned to the lowest level wp and not a responsible engineer
            var userIdsInLLWP = _context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId).Select(filtered => filtered.UserId);
            // just in case, check if the work package is in this project too
            return new JsonResult(_context.EmployeeProjects!.Where(ep => userIdsInLLWP.Contains(ep.UserId) && ep.ProjectId == HttpContext.Session.GetInt32("CurrentProject")).Select(e => e.User).Select(e => new { e!.Id, e.FirstName, e.LastName, e.JobTitle }));
        }

        // get employees with the project id
        [Authorize]
        public async Task<IActionResult> GetAssignedEmployeesAsync([FromBody] WorkPackage LowestLevelWp)
        {
            if (await verifyPMAsync() is IActionResult isPM) return isPM;

            return new JsonResult(_context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId && ewp.WorkPackageProjectId == HttpContext.Session.GetInt32("CurrentProject")).Select(e => e.User).Select(e => new { e!.Id, e.FirstName, e.LastName, e.JobTitle }));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
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

        public async Task<IActionResult> Report()
        {
            MemoryStream ms = new MemoryStream();

            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdfDoc = new PdfDocument(writer);
            Document document = new Document(pdfDoc, PageSize.A4, false);
            writer.SetCloseStream(false);

            Paragraph header = new Paragraph("Rate Sheet")
              .SetTextAlignment(TextAlignment.CENTER)
              .SetFontSize(20);

            document.Add(header);

            int fontSizeSH = 15;
            Paragraph subheader = new Paragraph($"Date of Issue: {DateTime.Now.ToShortDateString()}").SetFontSize(fontSizeSH);
            document.Add(subheader);

            Project? prj = await _context.Projects!.FindAsync(HttpContext.Session.GetInt32("CurrentProject"));
            ApplicationUser? mgr = await _context.Users.FindAsync(prj!.ProjectManagerId);
            if (prj != null)
            {
                document.Add(new Paragraph($"Project Name: {prj!.ProjectId}").SetFontSize(fontSizeSH));
                document.Add(new Paragraph($"Manager Name: {mgr!.FirstName} {mgr!.LastName}").SetFontSize(fontSizeSH));
                // document.Add(new Paragraph($"Manager Name: {prj!.ProjectManagerId}").SetFontSize(fontSizeSH));
            }

            // empty line
            document.Add(new Paragraph(""));

            // Line separator
            LineSeparator ls = new LineSeparator(new SolidLine());
            document.Add(ls);

            // empty line
            document.Add(new Paragraph(""));



            // Add table containing data
            document.Add(await GetPdfTable());

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
            fileStreamResult.FileDownloadName = "RateSheet.pdf";

            return fileStreamResult;
        }

        private async Task<Table> GetPdfTable()
        {
            // fetch data
            List<LabourGrade> lgs = await _context.LabourGrades!.ToListAsync();
            // Table with 2 columns
            Table table = new Table(2, false);
            // Headings
            Cell cellLabourCode = new Cell(1, 1)
               .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
               .SetTextAlignment(TextAlignment.CENTER)
               .Add(new Paragraph("Labour Grade Code"));

            Cell cellRates = new Cell(1, 1)
               .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
               .SetTextAlignment(TextAlignment.LEFT)
               .Add(new Paragraph("Rate"));

            //   Cell cellQuantity = new Cell(1, 1)
            //      .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
            //      .SetTextAlignment(TextAlignment.CENTER)
            //      .Add(new Paragraph("Estimate by Responsible Engineer"));

            table.AddCell(cellLabourCode);
            table.AddCell(cellRates);

            foreach (var item in lgs)
            {
                Cell cId = new Cell(1, 1)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .Add(new Paragraph(item.LabourCode!.ToString()));

                Cell cName = new Cell(1, 1)
                    .SetTextAlignment(TextAlignment.LEFT)
                    .Add(new Paragraph(item.Rate!.ToString()));

                table.AddCell(cId);
                table.AddCell(cName);
            }

            return table;
        }
        [Authorize]
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
                .Select(c => c.User)
                .Select(c => new { c!.FirstName, c!.LastName, c!.EmployeeNumber })
                .ToListAsync();
            return Json(employees);
        }
        [Authorize]
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
                proj.AssistantProjectManagerId = user;
                _context.SaveChanges();
                return Ok();
            }
            return new JsonResult("Error!");
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
        [Authorize]
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