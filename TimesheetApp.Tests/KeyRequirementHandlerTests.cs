using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TimesheetApp.Authorization;

namespace TimesheetApp.Tests;

public class KeyRequirementHandlerTests
{
    private static AuthorizationHandlerContext MakeContext(string? activatedValue)
    {
        var claims = activatedValue is null
            ? Array.Empty<Claim>()
            : new[] { new Claim("activated", activatedValue) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        return new AuthorizationHandlerContext(new[] { new KeyRequirement(true) }, principal, null);
    }

    [Fact]
    public async Task Succeeds_WhenActivatedClaimIsTrue()
    {
        var handler = new KeyRequirementHandler();
        var context = MakeContext("true");
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_WhenActivatedClaimIsFalse()
    {
        var handler = new KeyRequirementHandler();
        var context = MakeContext("false");
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_WhenActivatedClaimIsMissing()
    {
        var handler = new KeyRequirementHandler();
        var context = MakeContext(null);
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }
}
