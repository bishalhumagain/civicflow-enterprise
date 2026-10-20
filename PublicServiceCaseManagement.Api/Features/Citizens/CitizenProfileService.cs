using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Citizens;

public sealed class CitizenProfileService : ICitizenProfileService
{
    private readonly CivicFlowDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CitizenProfileService(CivicFlowDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<CitizenProfileResponse> CreateAsync(Guid applicationUserId, CreateCitizenProfileRequest request, CancellationToken cancellationToken)
    {
        if (applicationUserId == Guid.Empty)
            throw new ArgumentException("Application user ID is required.", nameof(applicationUserId));

        var existing = await _dbContext.CitizenProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == applicationUserId, cancellationToken);

        if (existing is not null)
            throw new InvalidOperationException("A citizen profile already exists for this user.");

        if (await _dbContext.CitizenProfiles.AnyAsync(p => p.Email == request.Email.Trim(), cancellationToken))
            throw new InvalidOperationException("A citizen profile with this email already exists.");

        if (!string.IsNullOrWhiteSpace(request.NIDNumber))
        {
            var nid = request.NIDNumber.Trim();
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.NIDNumber == nid, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this NID number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.CitizenshipNumber))
        {
            var cid = request.CitizenshipNumber.Trim();
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.CitizenshipNumber == cid, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this citizenship number already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        var profile = CitizenProfile.Create(
            applicationUserId,
            request.FullName,
            request.Email,
            request.PhoneNumber,
            request.NIDNumber,
            request.CitizenshipNumber,
            request.Age,
            request.Gender,
            request.PermanentAddress,
            request.TemporaryAddress,
            now);

        _dbContext.CitizenProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        if (!string.IsNullOrWhiteSpace(request.NIDNumber))
        {
            var nid = request.NIDNumber.Trim();
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.NIDNumber == nid && p.Id != profile.Id, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this NID number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.CitizenshipNumber))
        {
            var cid = request.CitizenshipNumber.Trim();
            if (await _dbContext.CitizenProfiles.AnyAsync(p => p.CitizenshipNumber == cid && p.Id != profile.Id, cancellationToken))
                throw new InvalidOperationException("A citizen profile with this citizenship number already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        profile.Update(
            request.PhoneNumber,
            request.NIDNumber,
            request.CitizenshipNumber,
            request.Age,
            request.Gender,
            request.PermanentAddress,
            request.TemporaryAddress,
            now);

        await _dbContext.SaveChangesAsync(cancellationToken);
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
