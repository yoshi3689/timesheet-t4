using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TimesheetApp.Data;
using TimesheetApp.Helpers;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config, ApplicationDbContext db)
        {
            _userManager = userManager;
            _config = config;
            _db = db;
        }

        public record LoginRequest(string Email, string Password);

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Ok(new { success = false, message = "Invalid email or password." });

            if (await _userManager.IsLockedOutAsync(user))
                return Ok(new { success = false, message = "Account locked out." });

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
            {
                await _userManager.AccessFailedAsync(user);
                return Ok(new { success = false, message = "Invalid email or password." });
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var tokenString = BuildJwt(roles, user);

            var rawRefreshToken = await IssueRefreshTokenAsync(user.Id);
            var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            Response.Cookies.Append("refreshToken", rawRefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = !isDev,
                SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                Path = "/api/auth",
            });

            return Ok(new { success = true, token = tokenString, message = "Logged in." });
        }

        // POST /api/auth/logout
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var rawToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(rawToken))
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
                var stored = await _db.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.RevokedAt == null);
                if (stored != null)
                {
                    stored.RevokedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
                Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/auth" });
            }
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

        // POST /api/auth/refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var rawToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(rawToken))
                return Unauthorized(new { message = "No refresh token." });

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            var stored = await _db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.RevokedAt == null);

            if (stored == null || !stored.IsActive)
                return Unauthorized(new { message = "Invalid or expired refresh token." });

            var roles = await _userManager.GetRolesAsync(stored.User!);
            var tokenString = BuildJwt(roles, stored.User!);

            return Ok(new { success = true, token = tokenString });
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

        private string BuildJwt(IList<string> roles, ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new("activated", (user.PublicKey != null).ToString().ToLower()),
            };
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var secret = _config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresHours = int.TryParse(_config["JWT_EXPIRES_HOURS"], out var h) ? h : 8;
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiresHours),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<string> IssueRefreshTokenAsync(string userId)
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = hash,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
            });
            await _db.SaveChangesAsync();
            return raw;
        }
    }
}
