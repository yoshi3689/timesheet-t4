using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private string CurrentProject;
        public ProjectController(ILogger<ProjectController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated && (User.IsInRole("HR") || User.IsInRole("Admin")))
            {
                var projects = _context.Projects.Include(s => s.ProjectManager);
                return View(projects);
            }
            else
            {
                var userId = _userManager.GetUserId(HttpContext.User);
                var project = _context.Projects.Where(s => s.ProjectManager.Id == userId).Include(s => s.ProjectManager);
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

        [Authorize]
        public IActionResult Edit(string? id)
        {
            CurrentProject = id;
            var workpackages = _context.WorkPackages.Where(c => c.ProjectId == id).Include(c => c.ResponsibleUser);
            return View(workpackages);
        }

        [Authorize(Roles = "HR,Admin")]
        public IActionResult Split(string? name)
        {
            Console.WriteLine("split:" + name);
            var workpackages = _context.WorkPackages.Where(c => c.ProjectId == CurrentProject).Include(c => c.ResponsibleUser);
            return View(workpackages);
        }


        [HttpPost]
        [Authorize(Roles = "HR,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("ProjectId,ProjectManagerId,MasterBudget")] Project project)
        {
            // Console.WriteLine(project.ProjectId, project.ProjectManagerId, project.MasterBudget);
            var newProject = new Project
            {
                ProjectId = project.ProjectId,
                ProjectManagerId = project.ProjectManagerId,
                MasterBudget = project.MasterBudget
            };
            _context.Projects.Add(newProject);
            _context.SaveChanges();
            var newWP = new WorkPackage
            {
                WorkPackageId = project.ProjectId,
                ProjectId = project.ProjectId,
                IsBottomLevel = true
            };
            _context.WorkPackages.Add(newWP);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}