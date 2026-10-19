namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class OrganizationTransactionMiddleware(
    RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IOrganizationTransactionContext transactionContext)
    {
        var succeeded = false;

        try
        {
            await next(httpContext);
            succeeded = httpContext.Response.StatusCode < 500;
        }
        finally
        {
            await transactionContext.CompleteAsync(succeeded);
        }
    }
}
