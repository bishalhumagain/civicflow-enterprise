namespace PublicServiceCaseManagement.Api.Domain.Cases;

/// <summary>
/// A free-text activity entry on a case. Public updates and information
/// requests form the citizen-facing conversation; internal notes are only
/// visible to authorized staff. The visibility flag is persisted so database
/// row-level security can enforce the same rule as the application.
/// </summary>
public sealed class CaseUpdate
{
    public const int MaximumMessageLength = 4000;

    private CaseUpdate()
    {
    }

    private CaseUpdate(
        Guid caseId,
        Guid organizationId,
        CaseUpdateKind kind,
        string message,
        Guid authorUserId,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        OrganizationId = organizationId;
        Kind = kind;
        Message = message;
        IsVisibleToCitizen = kind is not CaseUpdateKind.InternalNote;
        AuthorUserId = authorUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CaseId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public CaseUpdateKind Kind { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public bool IsVisibleToCitizen { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static CaseUpdate Create(
        Guid caseId,
        Guid organizationId,
        CaseUpdateKind kind,
        string message,
        Guid authorUserId,
        DateTimeOffset createdAt)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("Case ID is required.", nameof(caseId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "The author is required.",
                nameof(authorUserId));
        }

        var normalizedMessage = message?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedMessage) ||
            normalizedMessage.Length > MaximumMessageLength)
        {
            throw new ArgumentException(
                $"A message of at most {MaximumMessageLength} characters is " +
                "required.",
                nameof(message));
        }

        return new CaseUpdate(
            caseId,
            organizationId,
            kind,
            normalizedMessage,
            authorUserId,
            createdAt);
    }
}
