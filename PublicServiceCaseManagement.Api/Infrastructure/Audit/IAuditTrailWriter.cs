namespace PublicServiceCaseManagement.Api.Infrastructure.Audit;

/// <summary>
/// Persists audit events so they can be reviewed later instead of only being
/// written to the rolling application log.
/// </summary>
public interface IAuditTrailWriter
{
    Task WriteAsync(
        AuditRecord record,
        CancellationToken cancellationToken = default);
}
