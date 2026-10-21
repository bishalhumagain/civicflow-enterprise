namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Applies a conservative set of security response headers to every
/// response. The API returns JSON only, so a restrictive policy is safe.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var headers = httpContext.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["Content-Security-Policy"] =
            "default-src 'none'; frame-ancestors 'none'";
        headers["Permissions-Policy"] =
            "geolocation=(), camera=(), microphone=()";

        await next(httpContext);
    }
}
