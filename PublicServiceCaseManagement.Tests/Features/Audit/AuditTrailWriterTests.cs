using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Features.Audit;

public sealed class AuditTrailWriterTests
{
    private static (CivicFlowDbContext Context, AuditTrailWriter Writer)
        CreateWriter()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase($"AuditWriter_{Guid.NewGuid():N}")
            .Options;

        var context = new CivicFlowDbContext(options);

        return (context, new AuditTrailWriter(context, TimeProvider.System));
    }

    [Fact]
    public async Task WriteAsync_PersistsAuditEvent()
    {
        var (context, writer) = CreateWriter();

        await using var _ = context;

        var organizationId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();

        await writer.WriteAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = "organization.created",
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                ActorEmail = "admin@example.com",
                EntityType = "Organization",
                EntityId = organizationId.ToString(),
                Details = "Test Organization"
            });

        var stored = await context.AuditEvents.SingleAsync();

        Assert.Equal("Organization", stored.Category);
        Assert.Equal("organization.created", stored.Action);
        Assert.Equal(organizationId, stored.OrganizationId);
        Assert.Equal(actorUserId, stored.ActorUserId);
        Assert.Equal("admin@example.com", stored.ActorEmail);
        Assert.Equal("Organization", stored.EntityType);
        Assert.Equal("Test Organization", stored.Details);
    }

    [Fact]
    public async Task RecordAsync_AttributesTheEventToTheCurrentActor()
    {
        var writer = new CapturingAuditTrailWriter();
        var actorUserId = Guid.NewGuid();

        var logger = new AuditLogger(
            NullLogger<AuditLogger>.Instance,
            writer,
            new StubActorResolver(actorUserId, "actor@example.com"));

        await logger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountRegistered,
                ActorEmail = "citizen@example.com"
            });

        var record = Assert.Single(writer.Records);

        Assert.Equal(actorUserId, record.ActorUserId);
        Assert.Equal("citizen@example.com", record.ActorEmail);
    }

    [Fact]
    public async Task RecordAsync_KeepsTheActorThatWasSupplied()
    {
        var writer = new CapturingAuditTrailWriter();
        var suppliedActorId = Guid.NewGuid();

        var logger = new AuditLogger(
            NullLogger<AuditLogger>.Instance,
            writer,
            new StubActorResolver(Guid.NewGuid(), "actor@example.com"));

        await logger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountRegistered,
                ActorUserId = suppliedActorId,
                ActorEmail = "citizen@example.com"
            });

        var record = Assert.Single(writer.Records);

        Assert.Equal(suppliedActorId, record.ActorUserId);
    }

    [Fact]
    public async Task RecordAsync_DoesNotThrowWhenTheTrailIsUnavailable()
    {
        var logger = new AuditLogger(
            NullLogger<AuditLogger>.Instance,
            new FailingAuditTrailWriter(),
            new StubActorResolver(Guid.Empty, string.Empty));

        // The failure of the audit write must never break the operation it
        // describes, so this call has to complete without throwing.
        await logger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountRegistered,
                ActorEmail = "citizen@example.com"
            });
    }

    private sealed class CapturingAuditTrailWriter : IAuditTrailWriter
    {
        private readonly List<AuditRecord> _records = [];

        public IReadOnlyList<AuditRecord> Records => _records;

        public Task WriteAsync(
            AuditRecord record,
            CancellationToken cancellationToken = default)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingAuditTrailWriter : IAuditTrailWriter
    {
        public Task WriteAsync(
            AuditRecord record,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The audit trail is unavailable.");
    }

    private sealed class StubActorResolver(Guid userId, string email)
        : ICurrentActorResolver
    {
        public Task<CurrentActor?> ResolveAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CurrentActor?>(new CurrentActor(userId, email));
    }
}
