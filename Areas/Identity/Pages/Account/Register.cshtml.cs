// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimesheetApp.Controllers;
using TimesheetApp.Data;
using TimesheetApp.Helpers;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Areas.Identity.Pages.Account
{
    [Authorize(Roles = "Admin")]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            ApplicationDbContext context)
        {
            _context = context;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            this.roleManager = roleManager;
        }
        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }
        [BindProperty]
        public List<IdentityRole> rolesList { get; set; }
        public class InputModel
        {
            [Required]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            [Required]
            [Display(Name = "Employee Number")]
            [UniqueEmployeeNum]
            public int EmployeeNumber { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }


            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Temporary Password")]
            public string Password { get; set; }


            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Labour Grade")]
            // [Required]
            public string LabourGrade { get; set; }


            [Required]
            [Display(Name = "Job Title")]
            public string JobTitle { get; set; }
            [Required]
            public string Supervisor { get; set; }

            public List<string> AreTypes { get; set; } = new List<string>();
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ViewData["LabourGrades"] = new SelectList(_context.LabourGrades, "LabourCode", "LabourCode");
            ViewData["Supervisors"] = getSupervisors();
            rolesList = await roleManager.Roles.ToListAsync();
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        private SelectList getSupervisors()
        {
            var supervisors = _userManager.GetUsersInRoleAsync("Supervisor").GetAwaiter().GetResult().Select(s => new
            {
                Id = s.Id,
                Name = s.FirstName + " " + s.LastName
            });
            var hrs = _userManager.GetUsersInRoleAsync("HR").GetAwaiter().GetResult().Select(s => new
            {
                Id = s.Id,
                Name = s.FirstName + " " + s.LastName
            });
            var admins = _userManager.GetUsersInRoleAsync("Admin").GetAwaiter().GetResult().Select(s => new
            {
                Id = s.Id,
                Name = s.FirstName + " " + s.LastName
            });
            return new SelectList(supervisors.Concat(hrs).Concat(admins).ToList(), "Id", "Name");
        }


        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            string[] roles = Input.AreTypes.ToArray();
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();
                user.FirstName = Input.FirstName;
                user.LastName = Input.LastName;
                user.HasTempPassword = true;
                user.JobTitle = Input.JobTitle;
                user.LabourGradeCode = Input.LabourGrade;
                user.EmployeeNumber = Input.EmployeeNumber;
                user.EmailConfirmed = true;
                user.SupervisorId = Input.Supervisor;

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);
                await _userManager.AddToRolesAsync(user, roles);

                user.PublicKey = KeyHelper.CreateKeyPair(user.Id);
                _context.SaveChanges();


                if (result.Succeeded)
                {
                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            ViewData["LabourGrades"] = new SelectList(_context.LabourGrades, "LabourCode", "LabourCode");
            ViewData["Supervisors"] = getSupervisors();

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
