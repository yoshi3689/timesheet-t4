using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private string? CurrentProject;
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
            return View(new Project());
        }

        [Authorize(Roles = "HR,Admin")]
        public IActionResult Edit(string? id)
        {
            CurrentProject = id;
            HttpContext.Session.SetString("CurrentProject", id!);
            var workpackages = _context.WorkPackages!.Where(c => c.ProjectId == id).Include(c => c.ResponsibleUser).Include(c => c.ParentWorkPackage).Include(c => c.ChildWorkPackages);
            var top = workpackages.Where(c => c.ParentWorkPackage == null).FirstOrDefault()!;
            return View(findAllChildren(top));
        }

        private List<WorkPackage> findAllChildren(WorkPackage top)
        {
            List<WorkPackage> wps = new List<WorkPackage>();
            wps.Add(top);
            if (top.ChildWorkPackages == null || top.ChildWorkPackages.Count() == 0)
            {
                top = _context.WorkPackages!.Where(c => c.ProjectId == top.ProjectId && c.WorkPackageId == top.WorkPackageId).Include(c => c.ChildWorkPackages).FirstOrDefault()!;
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
        public IActionResult Split([FromBody] WorkPackage p)
        {
            CurrentProject = HttpContext.Session.GetString("CurrentProject");
            var parent = _context.WorkPackages!.Where(c => c.ProjectId == CurrentProject && c.WorkPackageId == p.ParentWorkPackageId).FirstOrDefault();
            if (parent == null)
            {
                return Json("Failed to find parent.");
            }
            parent.IsBottomLevel = false;
            var newChild = new WorkPackage
            {
                WorkPackageId = p.WorkPackageId,
                ProjectId = CurrentProject,
                ParentWorkPackageId = p.ParentWorkPackageId,
                ParentWorkPackageProjectId = CurrentProject,
                IsBottomLevel = true,
                IsClosed = false
            };

            if (_context.WorkPackages!.Where(c => c.ProjectId == CurrentProject && c.WorkPackageId == newChild.WorkPackageId).Count() == 0)
            {
                _context.WorkPackages!.Add(newChild);
                _context.SaveChanges();
                return Json(p.WorkPackageId);
            }
            else
            {
                return Json("Work Package must be unique for a project.");
            }
        }

        [Authorize(Roles = "HR,Admin")]
        public IActionResult GetDirectChildren([FromBody] WorkPackage parent)
        {
            CurrentProject = HttpContext.Session.GetString("CurrentProject");
            return new JsonResult(_context.WorkPackages!.Where(c => c.ProjectId == CurrentProject && c.ParentWorkPackageId == parent.WorkPackageId));
        }


        [HttpPost]
        [Authorize(Roles = "HR,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("ProjectId,ProjectManagerId,MasterBudget")] Project project)
        {
            if (ModelState.IsValid)
            {
                _context.Projects!.Add(project);
                _context.SaveChanges();

                //create a high level work package
                var newWP = new WorkPackage
                {
                    WorkPackageId = project.ProjectId,
                    ProjectId = project.ProjectId,
                    IsBottomLevel = true
                };
                _context.WorkPackages!.Add(newWP);
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
            string project = HttpContext.Session.GetString("CurrentProject")!;
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
    }

    public class UniqueProjectName : ValidationAttribute
    {
        public string GetErrorMessage() =>
            $"Project name must be unique";

        protected override ValidationResult? IsValid(
            object? value, ValidationContext validationContext)
        {
            string name = Convert.ToString(value)!;
            var _context = (ApplicationDbContext)validationContext.GetService(typeof(ApplicationDbContext))!;
            var user = _context.Projects!.Where(c => c.ProjectId == name);
            if (user.Count() == 0)
            {
                return ValidationResult.Success;
            }
            else
            {
                return new ValidationResult(GetErrorMessage());
            }
        }
    }

    public class WPFormat : ValidationAttribute
    {
        public string GetErrorMessage() =>
            $"Work Package ID must be in format [Letter][4xNumber]";

        protected override ValidationResult? IsValid(
            object? value, ValidationContext validationContext)
        {
            string name = Convert.ToString(value)!;

            if (Regex.IsMatch(name, "[a-zA-z]{1}[0-9]{4}"))
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