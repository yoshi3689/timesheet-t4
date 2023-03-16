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

namespace TimesheetApp.Controllers
{
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
                var project = _context.Projects!.Where(s => s.ProjectManager!.Id == userId).Include(s => s.ProjectManager);
                return View(project);
            }
        }

        [Authorize(Roles = "HR,Admin")]
        public IActionResult Create()
        {
            var users = _context.Users.Select(s => new
            {
                Id = s.Id,
                Name = s.FirstName + " " + s.LastName
            });
            ViewData["UserId"] = new SelectList(users, "Id", "Name");
            List<Budget> emptyBudgets = new List<Budget>();
            foreach (var item in _context.LabourGrades!.ToList())
            {
                emptyBudgets.Add(new Budget
                {
                    LabourCode = item.LabourCode,
                    LabourGrade = item,
                    isREBudget = false,
                    Rate = item.Rate,
                });
            }
            CreateProjectViewModel proj = new CreateProjectViewModel
            {
                budgets = emptyBudgets
            };
            return View(proj);
        }

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
                    WorkPackageId = Convert.ToString(input.project.ProjectId),
                    ProjectId = input.project.ProjectId,
                    IsBottomLevel = true,
                    Title = input.project.ProjectTitle
                };
                _context.WorkPackages!.Add(newWP);
                double totalBudget = 0;
                var grades = _context.LabourGrades;
                if (input.budgets != null)
                {
                    foreach (var budget in input.budgets)
                    {
                        Budget newBudget = new Budget
                        {
                            WPProjectId = input.project.ProjectId + "",
                            BudgetAmount = budget.BudgetAmount,
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
            return View();
        }

        [Authorize]
        public IActionResult Edit(int? id)
        {
            CurrentProject = id;
            HttpContext.Session.SetInt32("CurrentProject", id ?? 0);
            //find all work packages for the project and include the children so a tree can be made
            var workpackages = _context.WorkPackages!.Where(c => c.ProjectId == id).Include(c => c.ResponsibleUser).Include(c => c.ParentWorkPackage).Include(c => c.ChildWorkPackages);
            var top = workpackages.FirstOrDefault(c => c.ParentWorkPackage == null)!;

            var projectBudget = _context.Budgets.Where(c => c.WPProjectId == Convert.ToString(CurrentProject)).ToList();
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

            WorkPackageViewModel model = new WorkPackageViewModel
            {
                wps = findAllChildren(top),
                budgets = emptyBudgets,
            };
            return View(model);
        }

        private List<WorkPackage> findAllChildren(WorkPackage top)
        {
            List<WorkPackage> wps = new List<WorkPackage>();
            wps.Add(top);
            if (top.ChildWorkPackages == null || top.ChildWorkPackages.Count() == 0)
            {
                top = _context.WorkPackages!.Include(c => c.ChildWorkPackages).Include(c => c.ResponsibleUser).FirstOrDefault(c => c.ProjectId == top.ProjectId && c.WorkPackageId == top.WorkPackageId)!;
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


        [Authorize(Roles = "HR,Admin")]
        public IActionResult AssignEmployees([FromBody] List<EmployeeWorkPackage> ewps)
        {
            // string a = ewps[0].WorkPackageId!;
            foreach (var e in ewps)
            {
                _context.EmployeeWorkPackages!.Add(e);
            }
            _context.SaveChanges();
            // var userIdsInWp = _context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == a).Select(f => f.UserId);
            return new JsonResult(_context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == ewps[0].WorkPackageId));
        }

        [HttpPost]
        [Authorize(Roles = "HR,Admin")]
        public async Task<IActionResult> AssignResponsibleEngineerAsync([FromBody] EmployeeWorkPackage ewp)
        {
            var LLWP = await _context.WorkPackages.FindAsync(ewp.WorkPackageId, ewp.WorkPackageProjectId);
            Console.WriteLine(LLWP.ResponsibleUserId);
            LLWP.ResponsibleUserId = ewp.UserId;
            var user = _context.Users.Where(c => c.Id == ewp.UserId).First();
            _context.SaveChanges();
            return new JsonResult(user.FirstName + " " + user.LastName);
        }

        [Authorize(Roles = "HR,Admin")]
        public IActionResult Split(WorkPackageViewModel p)
        {
            CurrentProject = HttpContext.Session.GetInt32("CurrentProject");
            var parent = _context.WorkPackages!.FirstOrDefault(c => c.ProjectId == CurrentProject && c.WorkPackageId == p.WorkPackage.ParentWorkPackageId);
            if (parent != null)
            {
                parent.IsBottomLevel = false;
            }
            var newChild = new WorkPackage
            {
                WorkPackageId = p.WorkPackage.WorkPackageId,
                ProjectId = CurrentProject,
                ParentWorkPackageId = p.WorkPackage.ParentWorkPackageId,
                ParentWorkPackageProjectId = CurrentProject,
                IsBottomLevel = true,
                IsClosed = false,
                Title = p.WorkPackage.Title
            };

            if (_context.WorkPackages!.Where(c => c.ProjectId == CurrentProject && c.WorkPackageId == newChild.WorkPackageId).Count() != 0)
            {
                return Json("Work Package must be unique for a project.");
            }
            else if (newChild.WorkPackageId!.Contains("~"))
            {
                return Json("Reserved character '~'");
            }
            else
            {
                if (p.budgets != null)
                {
                    foreach (var budget in p.budgets)
                    {
                        Budget newBudget = new Budget
                        {
                            WPProjectId = CurrentProject + "~" + newChild.WorkPackageId,
                            BudgetAmount = budget.BudgetAmount,
                            LabourCode = budget.LabourCode,
                            Remaining = budget.BudgetAmount
                        };
                        _context.Budgets!.Add(newBudget);
                    }
                }
                _context.WorkPackages!.Add(newChild);
                _context.SaveChanges();
                newChild.ParentWorkPackage = null;
                if (newChild.ResponsibleUser == null)
                {
                    newChild.ResponsibleUser = new ApplicationUser
                    {
                        FirstName = null,
                        LastName = null
                    };
                }
                return Json(newChild);
            }
        }


        [Authorize(Roles = "HR,Admin")]
        public IActionResult GetDirectChildren([FromBody] WorkPackage parent)
        {
            CurrentProject = HttpContext.Session.GetInt32("CurrentProject");
            return new JsonResult(_context.WorkPackages!.Where(c => c.ProjectId == CurrentProject && c.ParentWorkPackageId == parent.WorkPackageId));
        }


        // get employees with the project id who are not assigned to the bottm lvl wpkg yet
        [Authorize(Roles = "HR,Admin")]
        public IActionResult GetAvailableEmployees([FromBody] WorkPackage LowestLevelWp)
        {
            // get empIds assigned to the lowest level wp
            var userIdsInLLWP = _context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId).Select(filtered => filtered.UserId);
            return new JsonResult(_context.EmployeeProjects!.Where(ep => !userIdsInLLWP.Contains(ep.UserId) && ep.ProjectId == HttpContext.Session.GetInt32("CurrentProject")).Select(e => e.User).Select(e => new { e.Id, e.FirstName, e.LastName, e.JobTitle }));
        }

        // get employees assigned to this lowest wpkg who are not a reponsible eng of this wpkg
        [Authorize(Roles = "HR,Admin")]
        public IActionResult GetCandidateEmployees([FromBody] WorkPackage LowestLevelWp)
        {

            // get empIds assigned to the lowest level wp and not a responsible engineer
            var userIdsInLLWP = _context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId && (ewp.WorkPackage!.ResponsibleUserId == null)).Select(filtered => filtered.UserId);
            // just in case, check if the work package is in this project too
            return new JsonResult(_context.EmployeeProjects!.Where(ep => userIdsInLLWP.Contains(ep.UserId) && ep.ProjectId == HttpContext.Session.GetInt32("CurrentProject")).Select(e => e.User).Select(e => new { e.Id, e.FirstName, e.LastName, e.JobTitle }));
        }

        // get employees with the project id
        [Authorize(Roles = "HR,Admin")]
        public IActionResult GetAssignedEmployees([FromBody] WorkPackage LowestLevelWp)
        {
            // get userIds of the users assigned to the lowest level wp
            var userIdsInLLWP = _context.EmployeeWorkPackages!.Where(ewp => ewp.WorkPackageId == LowestLevelWp.WorkPackageId).Select(filtered => filtered.UserId);

            // return
            return new JsonResult(_context.EmployeeProjects!.Where(ep => userIdsInLLWP.Contains(ep.UserId) && ep.ProjectId == HttpContext.Session.GetInt32("CurrentProject")).Select(e => e.User).Select(e => new { e.Id, e.FirstName, e.LastName, e.JobTitle }));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }



        //not sure if this is working right now
        [AcceptVerbs("Get", "Post")]
        public IActionResult CheckWorkPackage(string WorkPackageId)
        {
            Console.WriteLine(WorkPackageId);
            int? project = HttpContext.Session.GetInt32("CurrentProject");
            var wp = _context.WorkPackages!.Where(c => c.ProjectId == project && c.WorkPackageId == WorkPackageId);
            if (wp != null && wp.Count() > 0)
            {
                // return Json("true");
                return Json("false");
            }
            else
            {
                return Json("false");
            }
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
            Console.WriteLine(prj.ProjectId);
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
    }

    public class WPFormat : ValidationAttribute
    {
        public string GetErrorMessage() =>
            $"Work Package ID must be in format [Letter][4xNumber] and cannot contain \"~\"";

        protected override ValidationResult? IsValid(
            object? value, ValidationContext validationContext)
        {
            string name = Convert.ToString(value)!;

            if (Regex.IsMatch(name, "[a-zA-z]{1}[0-9]{4}") && !name.Contains("~"))
            {
                return ValidationResult.Success;
            }
            else
            {
                return new ValidationResult(GetErrorMessage());
            }
        }


    }

}