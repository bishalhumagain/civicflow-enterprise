
using System.Security.Claims;
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
        var userId = GetAuthenticatedUserId();

        if (userId is null)
        {
            return null;
        }

        return await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => user.Id == userId.Value && user.IsActive)
            .Select(user => (PlatformRole?)user.PlatformRole)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> ResolveApplicationUserIdAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetAuthenticatedUserId();

        if (userId is null)
        {
            return null;
        }

        return await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => user.Id == userId.Value && user.IsActive)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ResolvedMembership?> ResolveMembershipAsync(
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();

        if (userId is null)
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
                  && user.IsActive
                  && user.Id == userId.Value
            select new ResolvedMembership(
                membership.OrganizationId,
                membership.Role)
        ).SingleOrDefaultAsync(cancellationToken);
    }

    private Guid? GetAuthenticatedUserId()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var identifier = httpContext.User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(identifier, out var userId) ||
            userId == Guid.Empty)
        {
            return null;
        }

        return userId;
    }

    private sealed record ResolvedMembership(
        Guid OrganizationId,
        OrganizationRole Role);
}
