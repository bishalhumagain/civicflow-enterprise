using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

public sealed class AuditLogger(
    ILogger<AuditLogger> logger,
    IAuditTrailWriter auditTrailWriter,
    ICurrentActorResolver currentActorResolver) : IAuditLogger
{
    public async Task RecordAsync(
        AuditRecord record,
        CancellationToken cancellationToken = default)
    {
        var entry = await WithActorAsync(record, cancellationToken);

        logger.LogInformation(
            "AUDIT category={Category} action={Action} actorUserId={ActorUserId} " +
            "actorEmail={ActorEmail} organizationId={OrganizationId} " +
            "entityType={EntityType} entityId={EntityId} details={Details}",
            entry.Category,
            entry.Action,
            entry.ActorUserId,
            entry.ActorEmail,
            entry.OrganizationId,
            entry.EntityType,
            entry.EntityId,
            entry.Details);

        try
        {
            await auditTrailWriter.WriteAsync(entry, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A failing audit write must never take down the operation it
            // describes. The failure is reported loudly so it can be
            // investigated, but the request itself still completes.
            logger.LogError(
                exception,
                "AUDIT_PERSIST_FAILED action={Action} category={Category}",
                entry.Action,
                entry.Category);
        }
    }

    /// <summary>
    /// Attributes the event to the account that is making the request when
    /// the caller did not name an actor explicitly.
    /// </summary>
    private async Task<AuditRecord> WithActorAsync(
        AuditRecord record,
        CancellationToken cancellationToken)
    {
        if (record.ActorUserId.HasValue)
        {
            return record;
        }

        var actor = await currentActorResolver.ResolveAsync(
            cancellationToken);

        if (actor is null)
        {
            return record;
        }

        return record with
        {
            ActorUserId = actor.UserId,
            ActorEmail = string.IsNullOrWhiteSpace(record.ActorEmail)
                ? actor.Email
                : record.ActorEmail
        };
    }

    public async Task AuthenticationSucceededAsync(
        string email,
        Guid? userId,
        string context,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "AUTH_SUCCESS email={Email} userId={UserId} context={Context}",
            email,
            userId,
            context);

        await RecordAsync(
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
    }

    public async Task AuthenticationFailedAsync(
        string email,
        string reason,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "AUTH_FAILURE email={Email} reason={Reason}",
            email,
            reason);

        await RecordAsync(
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
    }

    public async Task AccountLifecycleAsync(
        string email,
        Guid? userId,
        string action,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "ACCOUNT_LIFECYCLE email={Email} userId={UserId} action={Action}",
            email,
            userId,
            action);

        await RecordAsync(
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
    }

    public async Task PrivilegeChangeAsync(
        Guid userId,
        string detail,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "PRIVILEGE_CHANGE userId={UserId} detail={Detail}",
            userId,
            detail);

        await RecordAsync(
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
}
