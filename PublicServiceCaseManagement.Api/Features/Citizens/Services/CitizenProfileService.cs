using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

using PublicServiceCaseManagement.Api.Domain.Common;

namespace PublicServiceCaseManagement.Api.Features.Citizens.Services;

public sealed class CitizenProfileService : ICitizenProfileService
{
    private readonly CivicFlowDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditLogger _auditLogger;

    public CitizenProfileService(
        CivicFlowDbContext dbContext,
        TimeProvider timeProvider,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<CitizenProfileResponse> CreateAsync(Guid applicationUserId, CreateCitizenProfileRequest request, CancellationToken cancellationToken)
    {
        if (applicationUserId == Guid.Empty)
            throw new ArgumentException("Application user ID is required.", nameof(applicationUserId));

        var email = TextNormalization.NormalizeEmail(
            request.Email,
            nameof(request.Email));
        var nid = TextNormalization.NormalizeOptionalIdentifier(
            request.NIDNumber);
        var cid = TextNormalization.NormalizeOptionalIdentifier(
            request.CitizenshipNumber);

        var existing = await _dbContext.CitizenProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == applicationUserId, cancellationToken);

        if (existing is not null)
            throw new InvalidOperationException("A citizen profile already exists for this user.");

        if (await _dbContext.CitizenProfiles.AnyAsync(p => p.Email == email, cancellationToken))
            throw new InvalidOperationException("A citizen profile with this email already exists.");

        if (!string.IsNullOrWhiteSpace(nid))
        {
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.NIDNumber == nid, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this NID number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(cid))
        {
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.CitizenshipNumber == cid, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this citizenship number already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        var profile = CitizenProfile.Create(
            applicationUserId,
            request.FullName,
            request.Email,
            request.PhoneNumber,
            nid,
            cid,
            request.Age,
            request.Gender,
            request.PermanentAddress,
            request.TemporaryAddress,
            now);

        _dbContext.CitizenProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountProfileUpdated,
                ActorUserId = applicationUserId,
                EntityType = "CitizenProfile",
                EntityId = profile.Id.ToString(),
                Details = "Citizen profile created."
            },
            cancellationToken);

        return Map(profile);
    }

    public async Task<CitizenProfileResponse?> GetByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.CitizenProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == applicationUserId, cancellationToken);

        return profile is null ? null : Map(profile);
    }

    public async Task<CitizenProfileResponse?> UpdateAsync(Guid applicationUserId, UpdateCitizenProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.CitizenProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == applicationUserId, cancellationToken);

        if (profile is null)
            return null;

        var nid = TextNormalization.NormalizeOptionalIdentifier(
            request.NIDNumber);
        var cid = TextNormalization.NormalizeOptionalIdentifier(
            request.CitizenshipNumber);

        if (!string.IsNullOrWhiteSpace(nid))
        {
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.NIDNumber == nid && p.Id != profile.Id, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this NID number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(cid))
        {
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.CitizenshipNumber == cid && p.Id != profile.Id, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this citizenship number already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        profile.Update(
            fullName: null,
            request.PhoneNumber,
            nid,
            cid,
            request.Age,
            request.Gender,
            request.PermanentAddress,
            request.TemporaryAddress,
            now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountProfileUpdated,
                ActorUserId = applicationUserId,
                EntityType = "CitizenProfile",
                EntityId = profile.Id.ToString(),
                Details = "Citizen profile updated."
            },
            cancellationToken);

        return Map(profile);
    }

    private static CitizenProfileResponse Map(CitizenProfile profile) =>
        new(
            profile.Id,
            profile.ApplicationUserId,
            profile.FullName,
            profile.Email,
            profile.PhoneNumber,
            profile.NIDNumber,
            profile.CitizenshipNumber,
            profile.Age,
            profile.Gender,
            profile.PermanentAddress,
            profile.TemporaryAddress,
            profile.CreatedAt,
            profile.UpdatedAt);
}
