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
using TimesheetApp.Services;

namespace TimesheetApp.Controllers
{
    public class EmployeeManagerController : Controller
    {
        private readonly IEmployeeService _employeeService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeeManagerController(IEmployeeService employeeService, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _employeeService = employeeService;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            var users = await _employeeService.GetAllEmployeesPaginatedAsync(page, pageSize);
            int totalCount = _employeeService.GetTotalEmployeeCount();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            ViewData["TotalPages"] = totalPages;
            ViewData["CurrentPage"] = page;
            ViewData["PageSize"] = pageSize;
            return View(users);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _employeeService.GetEmployeeDetailsAsync(id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            return View(applicationUser);
        }

        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _employeeService.FindEmployeeAsync(id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            var labourGrades = _employeeService.GetCurrentYearLabourGrades();
            ViewData["LabourGradeCode"] = new SelectList(labourGrades, "LabourCode", "LabourCode", applicationUser.LabourGradeCode);

            var employeesInRole = await _employeeService.GetSupervisorsAsync();
            var supervisors = employeesInRole.Select(c => new { Name = c.FirstName + " " + c.LastName, Id = c.Id });
            ViewData["SupervisorId"] = new SelectList(supervisors, "Id", "Name", applicationUser.SupervisorId);

            var timesheetApprovers = _employeeService.GetTimesheetApprovers(applicationUser.SupervisorId!);
            ViewData["TimesheetApproverId"] = new SelectList(timesheetApprovers, "Id", "Name", applicationUser.TimesheetApproverId);

            return View(applicationUser);
        }

        public IActionResult GetTimesheetApprovers(string supervisorId)
        {
            return Json(_employeeService.GetTimesheetApprovers(supervisorId));
        }

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
                    var user = await _employeeService.FindEmployeeAsync(id);
                    if (user != null)
                    {
                        await _employeeService.UpdateEmployeeAsync(user, applicationUser, _userManager);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_employeeService.EmployeeExists(applicationUser.Id))
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

            var labourGrades = _employeeService.GetCurrentYearLabourGrades();
            ViewData["LabourGradeCode"] = new SelectList(labourGrades, "LabourCode", "LabourCode", applicationUser.LabourGradeCode);
            ViewData["SupervisorId"] = new SelectList(_userManager.Users, "Id", "Id", applicationUser.SupervisorId);
            ViewData["TimesheetApproverId"] = new SelectList(_userManager.Users, "Id", "Id", applicationUser.TimesheetApproverId);
            return View(applicationUser);
        }
    }

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
