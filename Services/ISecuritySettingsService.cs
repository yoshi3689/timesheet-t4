using TimesheetApp.Models;

namespace TimesheetApp.Services;

public interface ISecuritySettingsService
{
    Task<bool> GetGlobalRequirementAsync();
    Task SetGlobalRequirementAsync(bool require);
    bool GetEffectiveRequirement(ApplicationUser user, bool globalRequire);
}
