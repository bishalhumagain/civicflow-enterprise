using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

/// <summary>
/// Reads the acting account from the authenticated request.
/// </summary>
public sealed class CurrentActorResolver(
    IHttpContextAccessor httpContextAccessor,
    CivicFlowDbContext dbContext) : ICurrentActorResolver
{
    public async Task<CurrentActor?> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var identifier = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        if (!Guid.TryParse(identifier, out var userId) ||
            userId == Guid.Empty)
        {
            return null;
        }

        var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;

        if (!string.IsNullOrWhiteSpace(email))
        {
            return new CurrentActor(userId, email);
        }

        var stored = await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(stored)
            ? null
            : new CurrentActor(userId, stored);
    }
}
