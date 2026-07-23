using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Tests;

public class AuthRefreshTokenTests
{
    private static ApplicationDbContext MakeDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static string HashToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    [Fact]
    public async Task FindActiveToken_ReturnsToken_WhenValidAndNotRevoked()
    {
        var db = MakeDb();
        var raw = "valid-token-abc123";
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = "user1",
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();

        var hash = HashToken(raw);
        var found = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.RevokedAt == null);

        Assert.NotNull(found);
        Assert.True(found.IsActive);
    }

    [Fact]
    public async Task FindActiveToken_ReturnsNull_WhenRevoked()
    {
        var db = MakeDb();
        var raw = "revoked-token-xyz";
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = "user1",
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            RevokedAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var hash = HashToken(raw);
        var found = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.RevokedAt == null);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindActiveToken_IsNotActive_WhenExpired()
    {
        var db = MakeDb();
        var raw = "expired-token-def";
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = "user1",
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var hash = HashToken(raw);
        var found = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.RevokedAt == null);

        Assert.NotNull(found);
        Assert.False(found.IsActive);
    }

    [Fact]
    public async Task RevokeToken_SetsRevokedAt()
    {
        var db = MakeDb();
        var raw = "to-be-revoked";
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = "user1",
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();

        var hash = HashToken(raw);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash);
        stored!.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var reloaded = await db.RefreshTokens.FindAsync(stored!.Id);
        Assert.NotNull(reloaded!.RevokedAt);
    }

    [Fact]
    public async Task Rotation_RevokesOldToken_AndIssuesActiveNewOne()
    {
        var db = MakeDb();
        var raw = "session-token-1";
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = "user1",
            TokenHash = HashToken(raw),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(13),
        });
        await db.SaveChangesAsync();

        var hash = HashToken(raw);
        var stored = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == hash);
        stored.RevokedAt = DateTime.UtcNow;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = "user1",
            TokenHash = HashToken("session-token-2"),
            CreatedAt = stored.CreatedAt,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
        });
        await db.SaveChangesAsync();

        var oldReloaded = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == hash);
        var newToken = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == HashToken("session-token-2"));
        Assert.NotNull(oldReloaded.RevokedAt);
        Assert.True(newToken.IsActive);
        Assert.Equal(stored.CreatedAt, newToken.CreatedAt);
    }

    [Fact]
    public async Task ReuseOfRevokedToken_RevokesAllActiveSiblingsForUser()
    {
        var db = MakeDb();
        db.RefreshTokens.AddRange(
            new RefreshToken
            {
                UserId = "user1",
                TokenHash = HashToken("rotated-away"),
                ExpiresAt = DateTime.UtcNow.AddDays(13),
                RevokedAt = DateTime.UtcNow.AddMinutes(-5),
            },
            new RefreshToken
            {
                UserId = "user1",
                TokenHash = HashToken("current-valid-token"),
                ExpiresAt = DateTime.UtcNow.AddDays(14),
            });
        await db.SaveChangesAsync();

        // Simulate the reuse-detection branch: presenting "rotated-away" again
        // must revoke every other still-active token belonging to the same user.
        var presented = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == HashToken("rotated-away"));
        Assert.NotNull(presented.RevokedAt);

        var siblings = await db.RefreshTokens
            .Where(rt => rt.UserId == presented.UserId && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var sibling in siblings) sibling.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var currentToken = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == HashToken("current-valid-token"));
        Assert.NotNull(currentToken.RevokedAt);
    }

    [Fact]
    public void SlidingExpiry_IsCappedByAbsoluteSessionStart()
    {
        const int slidingDays = 14;
        const int absoluteDays = 30;
        var sessionStartedAt = DateTime.UtcNow.AddDays(-25);

        var slidingExpiry = DateTime.UtcNow.AddDays(slidingDays);
        var absoluteExpiry = sessionStartedAt.AddDays(absoluteDays);
        var expiresAt = slidingExpiry < absoluteExpiry ? slidingExpiry : absoluteExpiry;

        // 25 days into the session, a 14-day slide would reach day 39, past the 30-day cap —
        // the absolute cap must win.
        Assert.Equal(absoluteExpiry, expiresAt);
        Assert.True(expiresAt < slidingExpiry);
    }
}
