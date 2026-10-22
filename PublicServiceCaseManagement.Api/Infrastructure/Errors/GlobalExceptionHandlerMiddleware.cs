using System.Net;
using System.Text.Json;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Infrastructure.Errors;

public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    IErrorLogger errorLogger,
    ICurrentActorResolver actorResolver,
    ICurrentOrganizationContext organizationContext)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var actor = await actorResolver.ResolveAsync();
        var userId = actor?.UserId;
        var organizationId = await organizationContext.GetOrganizationIdAsync();

        // Log the error to database
        await errorLogger.LogErrorAsync(exception, userId, organizationId, JsonSerializer.Serialize(new
        {
            Path = context.Request.Path,
            Method = context.Request.Method,
            QueryString = context.Request.QueryString.ToString()
        }));

        // Determine the user-friendly message
        var userMessage = GetUserFriendlyMessage(exception);
        var statusCode = GetStatusCode(exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = userMessage,
            statusCode = (int)statusCode,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private static string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => "You are not authorized to perform this action.",
            ArgumentException => "Invalid input provided. Please check your data and try again.",
            InvalidOperationException => "The operation could not be completed due to the current state.",
            KeyNotFoundException => "The requested resource was not found.",
            TimeoutException => "The operation timed out. Please try again later.",
            _ => "An unexpected error occurred. Please try again or contact support if the problem persists."
        };
    }

    private static HttpStatusCode GetStatusCode(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentException => HttpStatusCode.BadRequest,
            InvalidOperationException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            TimeoutException => HttpStatusCode.RequestTimeout,
            _ => HttpStatusCode.InternalServerError
        };
    }
}
