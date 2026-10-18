using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class AuthenticatedOrganizationResolver(
    IHttpContextAccessor httpContextAccessor,
    CivicFlowDbContext dbContext) : IAuthenticatedOrganizationResolver
{
    public async Task<Guid?> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null ||
            httpContext.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var issuer = httpContext.User.FindFirst("iss")?.Value;
        var subject = httpContext.User.FindFirst("sub")?.Value;

        var organizationHeader =
            httpContext.Request.Headers["X-Organization-Id"].ToString();

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(subject) ||
            !Guid.TryParse(organizationHeader, out var organizationId) ||
            organizationId == Guid.Empty)
        {
            return null;
        }

        var matchingOrganizationId = await (
            from membership in dbContext.OrganizationMemberships
            join user in dbContext.ApplicationUsers
                on membership.UserId equals user.Id
            join organization in dbContext.Organizations
                on membership.OrganizationId equals organization.Id
            where membership.OrganizationId == organizationId
                  && membership.IsActive
                  && organization.IsActive
                  && user.ExternalIdentityIssuer == issuer
                  && user.ExternalIdentitySubject == subject
            select membership.OrganizationId
        ).SingleOrDefaultAsync(cancellationToken);

        return matchingOrganizationId == Guid.Empty
            ? null
            : matchingOrganizationId;
    }
}