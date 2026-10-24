
using Microsoft.AspNetCore.Antiforgery;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Validates the antiforgery token for unsafe HTTP methods on API routes.
/// A token can be obtained from <c>GET /api/auth/csrf</c>. Requests without
/// a valid token are rejected with 400 before reaching controllers.
/// </summary>
public sealed class AntiforgeryMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Get,
            HttpMethods.Head,
            HttpMethods.Options,
            HttpMethods.Trace
        };

    public async Task InvokeAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        IConfiguration configuration)
    {
        var required = configuration.GetValue(
            "Security:RequireAntiforgery",
            defaultValue: true);

        if (!required ||
            !httpContext.Request.Path.StartsWithSegments("/api") ||
            SafeMethods.Contains(httpContext.Request.Method))
        {
            await next(httpContext);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            httpContext.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            // The `code` is a stable, machine-readable identifier that clients
            // key off of to refresh the anti-forgery token and retry. The
            // `error` string is human-facing only and must not be parsed.
            await httpContext.Response.WriteAsJsonAsync(new
            {
                code = AntiforgeryErrorCodes.InvalidToken,
                error = "The anti-forgery token is missing or invalid."
            });

            return;
        }

        await next(httpContext);
    }
}
