
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;
using PublicServiceCaseManagement.Tests.Infrastructure;

namespace PublicServiceCaseManagement.Tests.Features.Organizations;

public sealed class OrganizationServiceTests
{
    private static (
        CivicFlowDbContext Context,
        OrganizationService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase($"OrganizationTests_{Guid.NewGuid():N}")
            .Options;

        var context = new CivicFlowDbContext(options);

        return (context, new OrganizationService(context, new RecordingAuditLogger()));
    }

    [Fact]
    public async Task CreateAsync_CreatesOrganizationWithNormalizedCode()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var organization = await service.CreateAsync(
            " Kathmandu Metropolitan City ",
            " kathmandu-metro ",
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal("Kathmandu Metropolitan City", organization.Name);
        Assert.Equal("KATHMANDU-METRO", organization.Code);
        Assert.True(organization.IsActive);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateCodeIgnoringCase()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        await service.CreateAsync(
            "Organization One",
            "ORG-ONE",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                "Organization Two",
                "org-one",
                CancellationToken.None));

        Assert.Equal(
            "An organization with this code already exists.",
            exception.Message);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrganizationsOrderedByName()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        await service.CreateAsync(
            "Pokhara Metropolitan City",
            "POKHARA",
            CancellationToken.None);

        await service.CreateAsync(
            "Biratnagar Metropolitan City",
            "BIRATNAGAR",
            CancellationToken.None);

        var organizations = await service.GetAllAsync(
            CancellationToken.None);

        Assert.Equal(
            organizations.OrderBy(item => item.Name).Select(item => item.Name),
            organizations.Select(item => item.Name));
    }


    [Fact]
    public async Task GetByIdAsync_ReturnsOrganizationWhenItExists()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var created = await service.CreateAsync(
            "Organization One",
            "ORG-ONE",
            CancellationToken.None);

        var result = await service.GetByIdAsync(
            created.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenOrganizationDoesNotExist()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var result = await service.GetByIdAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesOrganizationDetails()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var created = await service.CreateAsync(
            "Old Name",
            "OLD-CODE",
            CancellationToken.None);

        var updated = await service.UpdateAsync(
            created.Id,
            "New Name",
            "new-code",
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("NEW-CODE", updated.Code);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_RejectsCodeUsedByAnotherOrganization()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var first = await service.CreateAsync(
            "Organization One",
            "ORG-ONE",
            CancellationToken.None);

        await service.CreateAsync(
            "Organization Two",
            "ORG-TWO",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(
                first.Id,
                "Organization One Updated",
                "ORG-TWO",
                CancellationToken.None));

        Assert.Equal(
            "An organization with this code already exists.",
            exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenOrganizationDoesNotExist()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            "Missing Organization",
            "MISSING",
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetActiveStatusAsync_DeactivatesAndReactivatesOrganization()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var created = await service.CreateAsync(
            "Organization One",
            "ORG-ONE",
            CancellationToken.None);

        var deactivated = await service.SetActiveStatusAsync(
            created.Id,
            false,
            CancellationToken.None);

        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);

        var reactivated = await service.SetActiveStatusAsync(
            created.Id,
            true,
            CancellationToken.None);

        Assert.NotNull(reactivated);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_ReturnsNullWhenOrganizationDoesNotExist()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var result = await service.SetActiveStatusAsync(
            Guid.NewGuid(),
            false,
            CancellationToken.None);

        Assert.Null(result);
    }
}
