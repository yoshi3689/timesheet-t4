using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TimesheetApp.Authorization;
/// <summary>
/// Object which stores the requirement value for if they need a key.
/// </summary>
public class KeyRequirement : IAuthorizationRequirement
{
    public KeyRequirement(bool hasKey)
    {
        HasKey = hasKey;
    }

    public bool HasKey { get; }
}
/// <summary>
/// verifies that the user has keys. They should not be allowed to view pages without having thier password set.
/// </summary>
public class KeyRequirementHandler : AuthorizationHandler<KeyRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext authContext, KeyRequirement requirement)
    {
        var activated = authContext.User.FindFirst("activated")?.Value == "true";
        if (activated == requirement.HasKey)
            authContext.Succeed(requirement);
        return Task.CompletedTask;
    }
}