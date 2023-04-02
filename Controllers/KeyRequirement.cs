using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Data;
using TimesheetApp.Models;

namespace TimesheetApp.Authorization;
public class KeyRequirement : IAuthorizationRequirement
{
    public KeyRequirement(bool hasKey)
    {
        HasKey = hasKey;
    }

    public bool HasKey { get; }
}

public class KeyRequirementHandler : AuthorizationHandler<KeyRequirement>
{
    private readonly ApplicationDbContext _dbContext;

    public KeyRequirementHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext authContext, KeyRequirement requirement)
    {
        var user = _dbContext.Users.Where(c => c.UserName == authContext.User.Identity!.Name).FirstOrDefault();
        if (user == null)
        {
            return Task.CompletedTask;
        }
        if ((user.PublicKey != null) == requirement.HasKey)
        {
            authContext.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}