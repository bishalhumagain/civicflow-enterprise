using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Audit;

/// <summary>
/// Writes audit events to the audit trail table. The writer deliberately
/// shares the request's database context so an audit entry is committed in
/// the same transaction as the change it describes.
/// </summary>
public sealed class AuditTrailWriter(
    CivicFlowDbContext dbContext,
    TimeProvider timeProvider) : IAuditTrailWriter
{
    public async Task WriteAsync(
        AuditRecord record,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = AuditEvent.Create(
            record.Category,
            record.Action,
            record.OrganizationId,
            record.ActorUserId,
            record.ActorEmail,
            record.EntityType,
            record.EntityId,
            record.Details,
            timeProvider.GetUtcNow());

        dbContext.AuditEvents.Add(auditEvent);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
