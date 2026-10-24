using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Enforces an absolute (hard) session lifetime for cookie-authenticated
/// requests, independent of the 30-minute sliding idle window. A session that
/// was issued longer than <c>Security:AbsoluteSessionLifetimeSeconds</c> ago is
/// rejected even if the bearer keeps refreshing it, so an idle-but-slid cookie
/// cannot live forever.
/// </summary>
/// <remarks>
/// This runs after authorization and only inspects the concrete Identity
/// application cookie, so personal access tokens and anonymous requests are
/// untouched. It is deliberately separate from the cookie
/// <c>OnValidatePrincipal</c> pipeline so it never interferes with Identity's
/// own security-stamp revalidation.
/// </remarks>
public sealed class SessionLifetimeMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    private const int DefaultAbsoluteLifetimeSeconds = 12 * 60 * 60;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var configured = configuration.GetValue(
            "Security:AbsoluteSessionLifetimeSeconds",
            defaultValue: DefaultAbsoluteLifetimeSeconds);

        var maxAge = TimeSpan.FromSeconds(configured);

        // A non-positive value disables the absolute cap (idle sliding expiry
        // still applies). Only act on a request that already carries an
        // authenticated cookie principal.
        if (maxAge > TimeSpan.Zero &&
            httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var result = await httpContext.AuthenticateAsync(
                IdentityConstants.ApplicationScheme);

            if (result.Succeeded &&
                result.Ticket is { } ticket &&
                IsExpired(
                    ticket.Properties?.IssuedUtc,
                    TimeProvider.System.GetUtcNow(),
                    maxAge))
            {
                await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

                httpContext.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                await httpContext.Response.WriteAsJsonAsync(new
                {
                    code = "session_expired",
                    error = "Your session has expired. Please sign in again."
                });

                return;
            }
        }

        await next(httpContext);
    }

    /// <summary>
    /// True when the cookie was issued more than <paramref name="maxAge"/>
    /// before <paramref name="nowUtc"/>. Kept as a pure, testable decision so
    /// the expiry logic does not depend on wall-clock timing in tests.
    /// </summary>
    public static bool IsExpired(
        DateTimeOffset? issuedUtc,
        DateTimeOffset nowUtc,
        TimeSpan maxAge)
    {
        if (issuedUtc is not { } issued || maxAge <= TimeSpan.Zero)
        {
            return false;
        }

        return nowUtc - issued > maxAge;
    }
}
