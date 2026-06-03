using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Helpers;
using TimesheetApp.Models;

namespace TimesheetApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public record LoginRequest(string Email, string Password);

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _signInManager.PasswordSignInAsync(
                request.Email, request.Password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
                return Ok(new { success = true, message = "Logged in." });

            if (result.IsLockedOut)
                return Ok(new { success = false, message = "Account locked out." });

            return Ok(new { success = false, message = "Invalid email or password." });
        }

        // POST /api/auth/logout
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { success = true });
        }

        // GET /api/auth/me
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                roles,
                employeeNumber = user.EmployeeNumber,
                labourGradeCode = user.LabourGradeCode,
                supervisorId = user.SupervisorId,
                hasTempPassword = user.HasTempPassword,
                timesheetApproverId = user.TimesheetApproverId
            });
        }
        public record ActivateRequest(string AccountPassword, string SignaturePassword);

        // POST /api/auth/activate
        [HttpPost("activate")]
        [Authorize]
        public async Task<IActionResult> Activate([FromBody] ActivateRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!user.HasTempPassword)
                return BadRequest(new { success = false, message = "Account already activated." });

            if (string.IsNullOrWhiteSpace(request.SignaturePassword))
                return BadRequest(new { success = false, message = "Signature password cannot be empty." });

            using var rsa = RSA.Create(2048);

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, resetToken, request.AccountPassword);
            if (!passwordResult.Succeeded)
                return BadRequest(new { success = false, message = passwordResult.Errors.First().Description });

            user.PublicKey = rsa.ExportRSAPublicKey();
            user.PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), request.SignaturePassword);
            user.HasTempPassword = false;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return StatusCode(500, new { success = false, message = "Failed to save user." });

            return Ok(new { success = true, message = "Account activated." });
        }
    }
}
