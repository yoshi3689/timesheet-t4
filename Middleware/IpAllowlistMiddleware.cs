using System.Net;

namespace TimesheetApp.Middleware;

public class IpAllowlistSettings
{
    public bool Enabled { get; init; }
    public bool LogOnly { get; init; } = true;
    public IReadOnlyList<IPNetwork> AllowedNetworks { get; init; } = Array.Empty<IPNetwork>();
}

public static class IpAllowlist
{
    public static bool IsAllowed(IPAddress? ip, IReadOnlyList<IPNetwork> networks)
    {
        if (ip is null) return false;
        foreach (var network in networks)
        {
            if (network.Contains(ip)) return true;
        }
        return false;
    }
}

/// <summary>
/// Network-layer gate in front of the existing auth stack. Runs after
/// UseForwardedHeaders so RemoteIpAddress reflects the real client, not
/// Cloud Run's edge proxy.
/// </summary>
public class IpAllowlistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IpAllowlistSettings _settings;
    private readonly ILogger<IpAllowlistMiddleware> _logger;
    private readonly bool _isDevelopment;

    public IpAllowlistMiddleware(
        RequestDelegate next,
        IpAllowlistSettings settings,
        ILogger<IpAllowlistMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _settings = settings;
        _logger = logger;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;

        // Cloud Run's startup/liveness probe target — never gated, at any point in rollout.
        if (path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        if (_isDevelopment && path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress;
        if (IpAllowlist.IsAllowed(ip, _settings.AllowedNetworks))
        {
            await _next(context);
            return;
        }

        if (_settings.LogOnly)
        {
            _logger.LogWarning("IP {Ip} would be blocked for {Path}", ip, path);
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
