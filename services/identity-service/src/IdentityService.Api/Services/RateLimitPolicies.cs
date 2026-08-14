using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace IdentityService.Api.Services;

/// <summary>
/// Rate-limit policies for authentication endpoints. All policies partition by
/// client IP so each caller gets its own bucket. Behind the YARP gateway the
/// client IP is read from the X-Forwarded-For header YARP adds; direct calls
/// (identity on port 5010) use the connection's remote IP.
/// </summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Register = "register";
    public const string Refresh = "refresh";
    public const string Password = "password";
    public const string ChangePassword = "change-password";

    private static readonly TimeSpan PerMinute = TimeSpan.FromMinutes(1);

    public static void Configure(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, _) =>
            {
                var retryAfter = TimeSpan.FromSeconds(60);
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadataRetryAfter))
                    retryAfter = metadataRetryAfter;
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
                Log.Warning(
                    "Rate limit exceeded for {Method} {Path} from {Ip}",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    GetClientIp(context.HttpContext));
                await Task.CompletedTask;
            };

            // Login brute-force protection: 10 attempts / minute / IP.
            options.AddPolicy(Login, context => RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(context), _ => Fixed(10, PerMinute)));

            // Account spam protection: 5 registrations / hour / IP.
            options.AddPolicy(Register, context => RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(context), _ => Fixed(5, TimeSpan.FromHours(1))));

            // Refresh tokens rotate on every call; parallel clients can burst. 30 / minute / IP.
            options.AddPolicy(Refresh, context => RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(context), _ => Fixed(30, PerMinute)));

            // Password-reset email flood protection (forgot + reset share this policy).
            options.AddPolicy(Password, context => RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(context), _ => Fixed(5, TimeSpan.FromMinutes(15))));

            // Authenticated password changes: 10 / 5 minutes / IP.
            options.AddPolicy(ChangePassword, context => RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(context), _ => Fixed(10, TimeSpan.FromMinutes(5))));
        });
    }

    private static FixedWindowRateLimiterOptions Fixed(int permitLimit, TimeSpan window) => new()
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true
    };

    /// <summary>
    /// Returns the caller's IP, honoring the X-Forwarded-For header set by the
    /// YARP gateway so limits are per-client and not per-gateway.
    /// </summary>
    public static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // Take the left-most (original client) address, strip any port.
            var ip = forwarded.Split(',')[0].Trim();
            var host = ip.Split(':')[0];
            if (host.Length > 0)
                return host;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
