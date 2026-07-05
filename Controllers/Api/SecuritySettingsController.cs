using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers.Api
{
    [ApiController]
    [Route("api/security-settings")]
    [Authorize(Policy = "KeyRequirement")]
    [Authorize(Roles = "Admin")]
    public class SecuritySettingsController : ControllerBase
    {
        private readonly ISecuritySettingsService _securitySettings;
        private readonly IConfiguration _config;

        public SecuritySettingsController(ISecuritySettingsService securitySettings, IConfiguration config)
        {
            _securitySettings = securitySettings;
            _config = config;
        }

        public record IpRestrictionStatusDto(bool Enabled, bool LogOnly, string[] AllowedCidrs);
        public record SecuritySettingsDto(bool Require2FA, IpRestrictionStatusDto IpRestriction);
        public record UpdateSecuritySettingsRequest(bool Require2FA);

        // GET /api/security-settings
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var require2FA = await _securitySettings.GetGlobalRequirementAsync();

            var ipEnabled = (_config["IP_RESTRICTION_ENABLED"] ?? "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);
            var ipLogOnly = (_config["IP_RESTRICTION_LOG_ONLY"] ?? "true")
                .Equals("true", StringComparison.OrdinalIgnoreCase);
            var allowedCidrs = (_config["IP_ALLOWED_CIDRS"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return Ok(new SecuritySettingsDto(
                require2FA,
                new IpRestrictionStatusDto(ipEnabled, ipLogOnly, allowedCidrs)));
        }

        // PUT /api/security-settings
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateSecuritySettingsRequest req)
        {
            await _securitySettings.SetGlobalRequirementAsync(req.Require2FA);
            return NoContent();
        }
    }
}
