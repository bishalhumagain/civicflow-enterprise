
namespace PublicServiceCaseManagement.Api.Domain.Cases;

public sealed class Case
{
    private Case()
    {
    }

    private Case(
        Guid organizationId,
        string referenceNumber,
        string title,
        string description,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ReferenceNumber = referenceNumber;
        Title = title;
        Description = description;
        Status = CaseStatus.Submitted;
        Priority = CasePriority.Normal;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string ReferenceNumber { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public CaseStatus Status { get; private set; }

    public CasePriority Priority { get; private set; }
    public Guid? AssignedToStaffMemberId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Case Create(
        Guid organizationId,
        string referenceNumber,
        string title,
        string description,
        DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(referenceNumber) ||
            referenceNumber.Length > 50)
        {
            throw new ArgumentException(
                "Reference number is required and must not exceed 50 characters.",
                nameof(referenceNumber));
        }

        ValidateDetails(title, description);

        return new Case(
            organizationId,
            referenceNumber.Trim(),
            title.Trim(),
            description.Trim(),
            now);
    }

    public void UpdateDetails(
        string title,
        string description,
        DateTimeOffset now)
    {
        EnsureCaseIsEditable();
        ValidateDetails(title, description);

        Title = title.Trim();
        Description = description.Trim();
        UpdatedAt = now;
    }

    public void ChangePriority(
        CasePriority priority,
        DateTimeOffset now)
    {
        EnsureCaseIsEditable();

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        Priority = priority;
        UpdatedAt = now;
    }

    public void ChangeStatus(
        CaseStatus newStatus,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus));
        }

        if (Status == newStatus)
        {
            return;
        }

        var isAllowed = Status switch
        {
            CaseStatus.Submitted =>
                newStatus is CaseStatus.UnderReview
                    or CaseStatus.Rejected,

            CaseStatus.UnderReview =>
                newStatus is CaseStatus.Assigned
                    or CaseStatus.AwaitingInformation
                    or CaseStatus.Rejected,

            CaseStatus.Assigned =>
                newStatus is CaseStatus.InProgress
                    or CaseStatus.AwaitingInformation
                    or CaseStatus.Rejected,

            CaseStatus.InProgress =>
                newStatus is CaseStatus.AwaitingInformation
                    or CaseStatus.Resolved,

            CaseStatus.AwaitingInformation =>
                newStatus is CaseStatus.UnderReview
                    or CaseStatus.Assigned
                    or CaseStatus.InProgress
                    or CaseStatus.Rejected,

            CaseStatus.Resolved =>
                newStatus == CaseStatus.Closed,

            CaseStatus.Closed or CaseStatus.Rejected => false,

            _ => false
        };

        if (!isAllowed)
        {
            throw new InvalidOperationException(
                $"A case cannot transition from {Status} to {newStatus}.");
        }

        Status = newStatus;
        UpdatedAt = now;
    }

    public void AssignTo(
        Guid staffMemberId,
        DateTimeOffset now)
    {
        EnsureCaseIsEditable();

        if (staffMemberId == Guid.Empty)
        {
            throw new ArgumentException(
                "Staff member ID is required.",
                nameof(staffMemberId));
        }

        if (AssignedToStaffMemberId == staffMemberId)
        {
            return;
        }

        AssignedToStaffMemberId = staffMemberId;
        UpdatedAt = now;
    }

    private void EnsureCaseIsEditable()
    {
        if (Status is CaseStatus.Closed or CaseStatus.Rejected)
        {
            throw new InvalidOperationException(
                $"A case in {Status} status cannot be edited.");
        }
    }

    private static void ValidateDetails(
        string title,
        string description)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new ArgumentException(
                "Title is required and must not exceed 200 characters.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description) ||
            description.Length > 5000)
        {
            throw new ArgumentException(
                "Description is required and must not exceed 5000 characters.",
                nameof(description));
        }
    }
}
