using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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

        public EmployeeManagerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: EmployeeManager
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Users.Include(a => a.LabourGrade).Include(a => a.Supervisor).Include(a => a.TimesheetApprover);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: EmployeeManager/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.Users
                .Include(a => a.LabourGrade)
                .Include(a => a.Supervisor)
                .Include(a => a.TimesheetApprover)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            return View(applicationUser);
        }

        // GET: EmployeeManager/Edit/5
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
            ViewData["LabourGradeCode"] = new SelectList(_context.LabourGrades, "LabourCode", "LabourCode", applicationUser.LabourGradeCode);
            ViewData["SupervisorId"] = new SelectList(_context.Users, "Id", "FirstName", applicationUser.SupervisorId);
            ViewData["TimesheetApproverId"] = new SelectList(_context.Users, "Id", "FirstName", applicationUser.TimesheetApproverId);
            return View(applicationUser);
        }

        // POST: EmployeeManager/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("FirstName,LastName,EmployeeNumber,SickDays,FlexTime,JobTitle,Salary,LabourGradeCode,SupervisorId,TimesheetApproverId,Id,UserName,NormalizedUserName,Email,NormalizedEmail,PhoneNumber,LockoutEnd,LockoutEnabled,AccessFailedCount")] ApplicationUser applicationUser)
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
                        user.SupervisorId = applicationUser.SupervisorId;
                        user.TimesheetApproverId = applicationUser.TimesheetApproverId;
                        user.UserName = applicationUser.UserName;
                        user.NormalizedUserName = user.UserName?.ToUpper();
                        user.Email = applicationUser.Email;
                        user.NormalizedEmail = user.Email?.ToUpper();
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
            ViewData["LabourGradeCode"] = new SelectList(_context.LabourGrades, "LabourCode", "LabourCode", applicationUser.LabourGradeCode);
            ViewData["SupervisorId"] = new SelectList(_context.Users, "Id", "Id", applicationUser.SupervisorId);
            ViewData["TimesheetApproverId"] = new SelectList(_context.Users, "Id", "Id", applicationUser.TimesheetApproverId);
            return View(applicationUser);
        }


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
            int num = Convert.ToInt32(value);
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
                int intValue = (int)value;
                int numDigits = intValue.ToString().Length;

                if (numDigits < MinLength || numDigits > MaxLength)
                {
                    return new ValidationResult($"Must be between {MinLength} and {MaxLength} digits long.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
