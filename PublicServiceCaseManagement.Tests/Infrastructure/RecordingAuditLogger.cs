using System.Collections.Concurrent;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Tests.Infrastructure;

/// <summary>
/// An audit logger that keeps every entry in memory so tests can assert on
/// what was recorded without touching the database.
/// </summary>
public sealed class RecordingAuditLogger : IAuditLogger
{
    private readonly ConcurrentQueue<AuditRecord> _records = new();

    public IReadOnlyList<AuditRecord> Records => _records.ToArray();

    public Task RecordAsync(
        AuditRecord record,
        CancellationToken cancellationToken = default)
    {
        _records.Enqueue(record);
        return Task.CompletedTask;
    }

    public Task AuthenticationSucceededAsync(
        string email,
        Guid? userId,
        string context,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Authentication,
                Action = AuditActions.AuthenticationSucceeded,
                ActorUserId = userId,
                ActorEmail = email,
                OrganizationId = organizationId,
                EntityType = "Account",
                EntityId = userId?.ToString(),
                Details = context
            },
            cancellationToken);

    public Task AuthenticationFailedAsync(
        string email,
        string reason,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Authentication,
                Action = AuditActions.AuthenticationFailed,
                ActorEmail = email,
                OrganizationId = organizationId,
                EntityType = "Account",
                Details = reason
            },
            cancellationToken);

    public Task AccountLifecycleAsync(
        string email,
        Guid? userId,
        string action,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = action,
                ActorUserId = userId,
                ActorEmail = email,
                OrganizationId = organizationId,
                EntityType = "Account",
                EntityId = userId?.ToString()
            },
            cancellationToken);

    public Task PrivilegeChangeAsync(
        Guid userId,
        string detail,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountPrivilegeChanged,
                ActorUserId = userId,
                OrganizationId = organizationId,
                EntityType = "Account",
                EntityId = userId.ToString(),
                Details = detail
            },
            cancellationToken);
}
