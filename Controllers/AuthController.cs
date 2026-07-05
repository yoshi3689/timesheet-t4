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
using TimesheetApp.Services;

namespace TimesheetApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly ISecuritySettingsService _securitySettings;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            ApplicationDbContext db,
            ISecuritySettingsService securitySettings)
        {
            _userManager = userManager;
            _config = config;
            _db = db;
            _securitySettings = securitySettings;
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

            var globalRequire2FA = await _securitySettings.GetGlobalRequirementAsync();
            var effectiveRequire2FA = _securitySettings.GetEffectiveRequirement(user, globalRequire2FA);

            if (effectiveRequire2FA && user.TwoFactorEnabled)
            {
                var preAuthToken = BuildPreAuthToken(user);
                return Ok(new { success = true, twoFactorRequired = true, preAuthToken });
            }

            return await IssueLoginResponseAsync(user);
        }

        public record Verify2FaRequest(string PreAuthToken, string Code);

        // POST /api/auth/login/verify-2fa
        [HttpPost("login/verify-2fa")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] Verify2FaRequest request)
        {
            var userId = ValidatePreAuthToken(request.PreAuthToken);
            if (userId == null)
                return Unauthorized(new { success = false, message = "Invalid or expired session. Please log in again." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { success = false, message = "Invalid or expired session. Please log in again." });

            bool codeValid;
            if (request.Code.Contains('-'))
            {
                var redeemResult = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, request.Code);
                codeValid = redeemResult.Succeeded;
            }
            else
            {
                codeValid = await _userManager.VerifyTwoFactorTokenAsync(
                    user, TokenOptions.DefaultAuthenticatorProvider, request.Code);
            }

            if (!codeValid)
                return Ok(new { success = false, message = "Invalid code." });

            return await IssueLoginResponseAsync(user);
        }

        private async Task<IActionResult> IssueLoginResponseAsync(ApplicationUser user)
        {
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
            var globalRequire2FA = await _securitySettings.GetGlobalRequirementAsync();
            var effectiveRequire2FA = _securitySettings.GetEffectiveRequirement(user, globalRequire2FA);

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
                timesheetApproverId = user.TimesheetApproverId,
                twoFactorEnabled = user.TwoFactorEnabled,
                needsTwoFactorSetup = effectiveRequire2FA && !user.TwoFactorEnabled
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

        public record TwoFactorSetupResponse(string SharedKey, string OtpauthUri);

        // POST /api/auth/2fa/setup
        [HttpPost("2fa/setup")]
        [Authorize]
        public async Task<IActionResult> SetupTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Reuse an existing not-yet-confirmed key rather than resetting on every call —
            // this endpoint gets hit more than once per enrollment in practice (React effect
            // double-invoke in dev, a page refresh mid-scan), and resetting each time silently
            // invalidates whatever QR code the user already scanned.
            var sharedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(sharedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                sharedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            var otpauthUri = $"otpauth://totp/{Uri.EscapeDataString("SHEET")}:{Uri.EscapeDataString(user.Email!)}" +
                $"?secret={sharedKey}&issuer={Uri.EscapeDataString("SHEET")}&digits=6";

            return Ok(new TwoFactorSetupResponse(sharedKey!, otpauthUri));
        }

        public record ConfirmTwoFactorRequest(string Code);
        public record ConfirmTwoFactorResponse(string[] RecoveryCodes);

        // POST /api/auth/2fa/confirm
        [HttpPost("2fa/confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmTwoFactor([FromBody] ConfirmTwoFactorRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var codeValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, TokenOptions.DefaultAuthenticatorProvider, request.Code);
            if (!codeValid)
                return BadRequest(new { success = false, message = "Invalid code." });

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

            return Ok(new ConfirmTwoFactorResponse(recoveryCodes!.ToArray()));
        }

        // POST /api/auth/2fa/disable
        [HttpPost("2fa/disable")]
        [Authorize]
        public async Task<IActionResult> DisableTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var globalRequire2FA = await _securitySettings.GetGlobalRequirementAsync();
            var effectiveRequire2FA = _securitySettings.GetEffectiveRequirement(user, globalRequire2FA);
            if (effectiveRequire2FA)
                return BadRequest(new { success = false, message = "Two-factor authentication is required by policy and cannot be disabled." });

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);

            return Ok(new { success = true });
        }

        private const string PreAuthStageClaim = "stage";
        private const string PreAuthStageValue = "2fa-pending";

        private string BuildPreAuthToken(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(PreAuthStageClaim, PreAuthStageValue),
            };

            var secret = _config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Deliberately validated by hand rather than routed through the shared JWT-bearer
        // [Authorize] pipeline, so this narrowly-scoped token can never be replayed against
        // any other endpoint even though it shares a signing key with the real session JWT.
        private string? ValidatePreAuthToken(string token)
        {
            var secret = _config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            try
            {
                var principal = handler.ValidateToken(token, validationParameters, out _);
                var stage = principal.FindFirstValue(PreAuthStageClaim);
                if (stage != PreAuthStageValue) return null;
                // JwtSecurityTokenHandler's default inbound claim map silently renames
                // "sub" to ClaimTypes.NameIdentifier during ValidateToken — FindFirstValue(sub)
                // returns null post-validation even though the token clearly has it.
                return principal.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            catch
            {
                return null;
            }
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
