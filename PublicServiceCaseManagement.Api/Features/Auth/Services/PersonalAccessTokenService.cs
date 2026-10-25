using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Api.Features.Auth.Contracts;

namespace PublicServiceCaseManagement.Api.Features.Auth.Services;

public interface IPersonalAccessTokenService
{
    Task<IReadOnlyList<PersonalAccessTokenResponse>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CreatePersonalAccessTokenResponse> CreateAsync(
        Guid userId,
        CreatePersonalAccessTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

public sealed class PersonalAccessTokenService(
    CivicFlowDbContext dbContext,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : IPersonalAccessTokenService
{
    private const int MaxActiveTokensPerUser = 25;

    public async Task<IReadOnlyList<PersonalAccessTokenResponse>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var tokens = await dbContext.PersonalAccessTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .OrderByDescending(token => token.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens.Select(token => Map(token, now)).ToList();
    }

    public async Task<CreatePersonalAccessTokenResponse> CreateAsync(
        Guid userId,
        CreatePersonalAccessTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var trimmedName = request.Name?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0)
        {
            throw new InvalidOperationException(
                "A token name is required.");
        }

        var activeCount = await dbContext.PersonalAccessTokens
            .CountAsync(
                token => token.UserId == userId &&
                         token.RevokedAt == null &&
                         (token.ExpiresAt == null || token.ExpiresAt > now),
                cancellationToken);

        if (activeCount >= MaxActiveTokensPerUser)
        {
            throw new InvalidOperationException(
                $"At most {MaxActiveTokensPerUser} active tokens are allowed. " +
                "Revoke one before creating another.");
        }

        var expiresAt = request.ExpiresInDays is { } days
            ? now.AddDays(days)
            : (DateTimeOffset?)null;

        var (token, plaintext) = PersonalAccessToken.Create(
            userId,
            trimmedName,
            now,
            expiresAt);

        dbContext.PersonalAccessTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.AccessToken,
                Action = AuditActions.AccessTokenCreated,
                ActorUserId = userId,
                EntityType = "PersonalAccessToken",
                EntityId = token.Id.ToString(),
                Details =
                    $"name={token.Name} publicId={token.PublicId} " +
                    $"expiresAt={token.ExpiresAt?.ToString("O") ?? "never"}"
            },
            cancellationToken);

        return new CreatePersonalAccessTokenResponse(
            token.Id,
            token.Name,
            plaintext,
            token.CreatedAt,
            token.ExpiresAt);
    }

    public async Task<bool> RevokeAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await dbContext.PersonalAccessTokens
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == tokenId && candidate.UserId == userId,
                cancellationToken);

        if (token is null)
        {
            return false;
        }

        if (token.IsRevoked)
        {
            return true;
        }

        token.Revoke(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.AccessToken,
                Action = AuditActions.AccessTokenRevoked,
                ActorUserId = userId,
                EntityType = "PersonalAccessToken",
                EntityId = token.Id.ToString(),
                Details = $"name={token.Name} publicId={token.PublicId}"
            },
            cancellationToken);

        return true;
    }

    private static PersonalAccessTokenResponse Map(
        PersonalAccessToken token,
        DateTimeOffset now) =>
        new(
            token.Id,
            token.Name,
            token.PublicId,
            token.CreatedAt,
            token.ExpiresAt,
            token.LastUsedAt,
            token.RevokedAt,
            token.IsActive(now));
}
