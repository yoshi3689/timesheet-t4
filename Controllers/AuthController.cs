using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using TimesheetApp.Helpers;
using TimesheetApp.Models;

namespace TimesheetApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config)
        {
            _userManager = userManager;
            _config = config;
        }

        public record LoginRequest(string Email, string Password);

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                return Ok(new { success = false, message = "Invalid email or password." });

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim("activated", (user.PublicKey != null).ToString().ToLower()),
            };
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var secret = _config["JWT_SECRET"] ?? "dev-secret-key-must-be-at-least-32-characters!";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresHours = int.TryParse(_config["JWT_EXPIRES_HOURS"], out var h) ? h : 8;
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiresHours),
                signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { success = true, token = tokenString, message = "Logged in." });
        }

        // POST /api/auth/logout
        [HttpPost("logout")]
        public IActionResult Logout() => Ok(new { success = true });

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
