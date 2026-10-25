
namespace PublicServiceCaseManagement.Api.Domain.Cases;

public sealed class CaseStatusHistory
{
    public const int MaximumExplanationLength = 2000;

    private CaseStatusHistory()
    {
    }

    private CaseStatusHistory(
        Guid caseId,
        Guid organizationId,
        CaseStatus previousStatus,
        CaseStatus newStatus,
        DateTimeOffset changedAt,
        Guid? changedByUserId,
        string? explanation,
        bool isVisibleToCitizen)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        OrganizationId = organizationId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedAt = changedAt;
        ChangedByUserId = changedByUserId;
        Explanation = explanation;
        IsVisibleToCitizen = isVisibleToCitizen;
    }

    public Guid Id { get; private set; }

    public Guid CaseId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public CaseStatus PreviousStatus { get; private set; }

    public CaseStatus NewStatus { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    /// <summary>
    /// Optional human-readable context for the transition. It is only exposed
    /// to the citizen when <see cref="IsVisibleToCitizen"/> is true, which
    /// keeps internal staff commentary out of the public timeline.
    /// </summary>
    public string? Explanation { get; private set; }

    public bool IsVisibleToCitizen { get; private set; }

    public static CaseStatusHistory Create(
        Guid caseId,
        Guid organizationId,
        CaseStatus previousStatus,
        CaseStatus newStatus,
        DateTimeOffset changedAt,
        Guid? changedByUserId = null,
        string? explanation = null,
        bool isVisibleToCitizen = false)
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

        var normalizedExplanation = string.IsNullOrWhiteSpace(explanation)
            ? null
            : explanation.Trim();

        if (normalizedExplanation is { Length: > MaximumExplanationLength })
        {
            throw new ArgumentException(
                $"The status explanation must not exceed " +
                $"{MaximumExplanationLength} characters.",
                nameof(explanation));
        }

        return new CaseStatusHistory(
            caseId,
            organizationId,
            previousStatus,
            newStatus,
            changedAt,
            changedByUserId,
            normalizedExplanation,
            normalizedExplanation is not null && isVisibleToCitizen);
    }
}
