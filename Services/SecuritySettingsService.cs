using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public class SecuritySettingsService : ISecuritySettingsService
{
    private readonly ApplicationDbContext _context;

    public SecuritySettingsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> GetGlobalRequirementAsync()
    {
        var settings = await GetOrCreateSettingsAsync();
        return settings.Require2FA;
    }

    public async Task SetGlobalRequirementAsync(bool require)
    {
        var settings = await GetOrCreateSettingsAsync();
        settings.Require2FA = require;
        await _context.SaveChangesAsync();
    }

    public bool GetEffectiveRequirement(ApplicationUser user, bool globalRequire)
    {
        return user.TwoFactorPolicyOverride ?? globalRequire;
    }

    private async Task<SystemSettings> GetOrCreateSettingsAsync()
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SystemSettings { Require2FA = false };
            _context.SystemSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }
}
