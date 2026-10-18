
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class AuthenticatedOrganizationResolver(
    IHttpContextAccessor httpContextAccessor,
    CivicFlowDbContext dbContext) : IAuthenticatedOrganizationResolver
{
    public async Task<Guid?> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        var membership = await ResolveMembershipAsync(cancellationToken);

        return membership?.OrganizationId;
    }

    public async Task<OrganizationRole?> ResolveRoleAsync(
        CancellationToken cancellationToken = default)
    {
        var membership = await ResolveMembershipAsync(cancellationToken);

        return membership?.Role;
    }

    public async Task<PlatformRole?> ResolvePlatformRoleAsync(
        CancellationToken cancellationToken = default)
    {
        var identity = GetAuthenticatedIdentity();

        if (identity is null)
        {
            return null;
        }

        return await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user =>
                user.ExternalIdentityIssuer == identity.Value.Issuer &&
                user.ExternalIdentitySubject == identity.Value.Subject)
            .Select(user => (PlatformRole?)user.PlatformRole)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ResolvedMembership?> ResolveMembershipAsync(
        CancellationToken cancellationToken)
    {
        var identity = GetAuthenticatedIdentity();

        if (identity is null)
        {
            return null;
        }

        var organizationHeader = httpContextAccessor.HttpContext!
            .Request.Headers["X-Organization-Id"]
            .ToString();

        if (!Guid.TryParse(organizationHeader, out var organizationId) ||
            organizationId == Guid.Empty)
        {
            return null;
        }

        return await (
            from membership in dbContext.OrganizationMemberships
            join user in dbContext.ApplicationUsers
                on membership.UserId equals user.Id
            join organization in dbContext.Organizations
                on membership.OrganizationId equals organization.Id
            where membership.OrganizationId == organizationId
                  && membership.IsActive
                  && organization.IsActive
                  && user.ExternalIdentityIssuer == identity.Value.Issuer
                  && user.ExternalIdentitySubject == identity.Value.Subject
            select new ResolvedMembership(
                membership.OrganizationId,
                membership.Role)
        ).SingleOrDefaultAsync(cancellationToken);
    }

    private (string Issuer, string Subject)? GetAuthenticatedIdentity()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var issuer = httpContext.User.FindFirst("iss")?.Value;
        var subject = httpContext.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        return (issuer, subject);
    }

    private sealed record ResolvedMembership(
        Guid OrganizationId,
        OrganizationRole Role);
}
