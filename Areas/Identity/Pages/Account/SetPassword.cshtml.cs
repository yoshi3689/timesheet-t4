using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using TimesheetApp.Data;
using TimesheetApp.Helpers;
using TimesheetApp.Models;

namespace TimesheetApp.Areas.Identity.Pages.Account
{
    public class SetPassword : PageModel
    {
        private readonly ILogger<SetPassword> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SetPassword(ILogger<SetPassword> logger, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _logger = logger;
            _userManager = userManager;
            _context = context;
            Input = new InputModel();
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {

            [Required]
            [DataType(DataType.Password)]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
            public string? Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string? ConfirmPassword { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
            public string? SignaturePassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("SignaturePassword", ErrorMessage = "The signature password and confirmation password do not match.")]
            public string? SignatureConfirmPassword { get; set; }
        }



        public void OnGet(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                if (Input!.Password == Input.ConfirmPassword && Input.SignaturePassword == Input.SignatureConfirmPassword)
                {
                    RSA rsa = RSA.Create();
                    var user = await _userManager.GetUserAsync(User);
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user!);
                    var result = await _userManager.ResetPasswordAsync(user!, token, Input.Password!);
                    user!.HasTempPassword = false;
                    user.PublicKey = rsa.ExportRSAPublicKey();
                    user.PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), Input.SignaturePassword!);
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
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}