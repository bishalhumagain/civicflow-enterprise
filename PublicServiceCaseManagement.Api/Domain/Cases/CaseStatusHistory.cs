
namespace PublicServiceCaseManagement.Api.Domain.Cases;

public sealed class CaseStatusHistory
{
    private CaseStatusHistory()
    {
    }

    private CaseStatusHistory(
        Guid caseId,
        Guid organizationId,
        CaseStatus previousStatus,
        CaseStatus newStatus,
        DateTimeOffset changedAt,
        Guid? changedByUserId)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        OrganizationId = organizationId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedAt = changedAt;
        ChangedByUserId = changedByUserId;
    }

    public Guid Id { get; private set; }

    public Guid CaseId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public CaseStatus PreviousStatus { get; private set; }

    public CaseStatus NewStatus { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public static CaseStatusHistory Create(
        Guid caseId,
        Guid organizationId,
        CaseStatus previousStatus,
        CaseStatus newStatus,
        DateTimeOffset changedAt,
        Guid? changedByUserId = null)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException(
                "Case ID is required.",
                nameof(caseId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        if (!Enum.IsDefined(previousStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousStatus));
        }

        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus));
        }

        if (previousStatus == newStatus)
        {
            throw new ArgumentException(
                "A status history record requires a status change.");
        }

        return new CaseStatusHistory(
            caseId,
            organizationId,
            previousStatus,
            newStatus,
            changedAt,
            changedByUserId);
    }
}
