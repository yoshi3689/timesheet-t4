using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;

namespace TimesheetApp.Controllers
{
    public class EmployeeManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeeManagerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Get a page with a list of all the employees in the system.
        /// </summary>
        /// <returns>page with all employees</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            var applicationDbContext = _context.Users.Include(a => a.Supervisor).Include(a => a.TimesheetApprover).OrderBy(a => a.FirstName); ;

            // Calculate the number of users to skip based on the current page and page size
            int skip = (page - 1) * pageSize;

            // Retrieve the subset of users based on the page size and skip count
            var users = await applicationDbContext.Skip(skip).Take(pageSize).ToListAsync();

            // Calculate the total number of pages based on the total number of users and page size
            int totalPages = (int)Math.Ceiling(applicationDbContext.Count() / (double)pageSize);

            // Pass the users and pagination information to the view
            ViewData["TotalPages"] = totalPages;
            ViewData["CurrentPage"] = page;
            ViewData["PageSize"] = pageSize;
            return View(users);
        }

        /// <summary>
        /// Get the details about an employee
        /// </summary>
        /// <param name="id">employee id</param>
        /// <returns>details page</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Details(string id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.Users
                .Include(a => a.Supervisor)
                .Include(a => a.TimesheetApprover)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            return View(applicationUser);
        }

        /// <summary>
        /// get the page where you can edit the details about an employee
        /// </summary>
        /// <param name="id">id of employee to edit</param>
        /// <returns>page to edit</returns>
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.Users.FindAsync(id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            ViewData["LabourGradeCode"] = new SelectList(_context.LabourGrades.Where(c => c.Year == DateTime.Now.Year), "LabourCode", "LabourCode", applicationUser.LabourGradeCode);

            // Get the list of supervisors
            var employeesInRole = await _userManager.GetUsersInRoleAsync("Supervisor");
            var supervisors = employeesInRole.Select(c => new
            {
                Name = c.FirstName + " " + c.LastName,
                Id = c.Id
            });
            ViewData["SupervisorId"] = new SelectList(supervisors, "Id", "Name", applicationUser.SupervisorId);

            // Get the list of timesheet approvers based on the selected supervisor
            var selectedSupervisorId = applicationUser.SupervisorId;
            var timesheetApprovers = _context.Users
                .Where(c => c.SupervisorId == selectedSupervisorId || c.Id == selectedSupervisorId)
                .Select(c => new
                {
                    Name = c.FirstName + " " + c.LastName,
                    Id = c.Id
                });
            ViewData["TimesheetApproverId"] = new SelectList(timesheetApprovers, "Id", "Name", applicationUser.TimesheetApproverId);

            return View(applicationUser);
        }

        /// <summary>
        /// Endpoint to get a list of timesheet approvers if you change the supervisor
        /// </summary>
        /// <param name="supervisorId"></param>
        /// <returns></returns>
        public IActionResult GetTimesheetApprovers(string supervisorId)
        {
            var timesheetApprovers = _context.Users
                .Where(c => c.SupervisorId == supervisorId || c.Id == supervisorId)
                .Select(c => new
                {
                    Name = c.FirstName + " " + c.LastName,
                    Id = c.Id
                })
                .ToList();

            return Json(timesheetApprovers);
        }

        /// <summary>
        /// POST enddpoint where user is created with the details from the page
        /// </summary>
        /// <param name="id">id of the employee</param>
        /// <param name="applicationUser">details to update</param>
        /// <returns>redirect to index</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Edit(string id, [Bind("FirstName,LastName,EmployeeNumber,SickDays,FlexTime,JobTitle,Salary,LabourGradeCode,SupervisorId,TimesheetApproverId,Id,UserName,NormalizedUserName,UserName,Email,NormalizedEmail,PhoneNumber,LockoutEnd,LockoutEnabled,AccessFailedCount")] ApplicationUser applicationUser)
        {
            if (id != applicationUser.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Retrieve the user from the database
                    var user = await _context.Users.FindAsync(id);
                    if (user != null)
                    {
                        user.FirstName = applicationUser.FirstName;
                        user.LastName = applicationUser.LastName;
                        user.EmployeeNumber = applicationUser.EmployeeNumber;
                        user.SickDays = applicationUser.SickDays;
                        user.FlexTime = applicationUser.FlexTime;
                        user.JobTitle = applicationUser.JobTitle;
                        user.Salary = applicationUser.Salary;
                        user.LabourGradeCode = applicationUser.LabourGradeCode;
                        if (await _userManager.IsInRoleAsync(await _userManager.FindByIdAsync(applicationUser.SupervisorId!) ?? new ApplicationUser(), "Supervisors") && user.Id != applicationUser.SupervisorId)
                        {
                            user.SupervisorId = applicationUser.SupervisorId;
                        }
                        var approver = await _userManager.FindByIdAsync(applicationUser.TimesheetApproverId!);
                        if (approver != null && applicationUser.TimesheetApproverId != user.Id && (approver.SupervisorId == user.SupervisorId || approver.Id == user.SupervisorId))
                        {
                            user.TimesheetApproverId = applicationUser.TimesheetApproverId;
                        }
                        user.PhoneNumber = applicationUser.PhoneNumber;
                        user.LockoutEnd = applicationUser.LockoutEnd;
                        user.LockoutEnabled = applicationUser.LockoutEnabled;
                        user.AccessFailedCount = applicationUser.AccessFailedCount;

                        _context.Update(user);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ApplicationUserExists(applicationUser.Id))
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
            ViewData["LabourGradeCode"] = new SelectList(_context.LabourGrades.Where(c => c.Year == DateTime.Now.Year), "LabourCode", "LabourCode", applicationUser.LabourGradeCode);
            ViewData["SupervisorId"] = new SelectList(_context.Users, "Id", "Id", applicationUser.SupervisorId);
            ViewData["TimesheetApproverId"] = new SelectList(_context.Users, "Id", "Id", applicationUser.TimesheetApproverId);
            return View(applicationUser);
        }

        /// <summary>
        /// check if a user already exists
        /// </summary>
        /// <param name="id">id to check</param>
        /// <returns>true if exists</returns>
        private bool ApplicationUserExists(string id)
        {
            return (_context.Users?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }

    /// <summary>
    /// This class is used to verify that an employee has a unique employee num. It is used as an annotation in the ApplicationUser class.
    /// </summary>
    public class UniqueEmployeeNum : ValidationAttribute
    {
        public string GetErrorMessage() =>
            $"Employee number must be unique";

        protected override ValidationResult? IsValid(
            object? value, ValidationContext validationContext)
        {
            long num = Convert.ToInt64(value);
            var _context = (ApplicationDbContext)validationContext.GetService(typeof(ApplicationDbContext))!;
            var user = _context.Users.Where(c => c.EmployeeNumber == num);
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

    /// <summary>
    /// used to verify the length of a number.
    /// </summary>
    public class IntLengthAttribute : ValidationAttribute
    {
        public int MinLength { get; set; }
        public int MaxLength { get; set; }

        public IntLengthAttribute(int minLength, int maxLength)
        {
            MinLength = minLength;
            MaxLength = maxLength;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value != null)
            {
                long intValue = Convert.ToInt64(value);
                long numDigits = intValue.ToString().Length;

                if (numDigits < MinLength || numDigits > MaxLength)
                {
                    return new ValidationResult($"Must be between {MinLength} and {MaxLength} digits long.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
