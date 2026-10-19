
namespace PublicServiceCaseManagement.Api.Domain.Cases;

public sealed class CaseAssignmentHistory
{
    private CaseAssignmentHistory()
    {
    }

    private CaseAssignmentHistory(
        Guid caseId,
        Guid organizationId,
        Guid? previousStaffMemberId,
        Guid newStaffMemberId,
        DateTimeOffset assignedAt,
        Guid? assignedByUserId)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        OrganizationId = organizationId;
        PreviousStaffMemberId = previousStaffMemberId;
        NewStaffMemberId = newStaffMemberId;
        AssignedAt = assignedAt;
        AssignedByUserId = assignedByUserId;
    }

    public Guid Id { get; private set; }

    public Guid CaseId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? PreviousStaffMemberId { get; private set; }

    public Guid NewStaffMemberId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public Guid? AssignedByUserId { get; private set; }

    public static CaseAssignmentHistory Create(
        Guid caseId,
        Guid organizationId,
        Guid? previousStaffMemberId,
        Guid newStaffMemberId,
        DateTimeOffset assignedAt,
        Guid? assignedByUserId = null)
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

        if (newStaffMemberId == Guid.Empty)
        {
            throw new ArgumentException(
                "New staff member ID is required.",
                nameof(newStaffMemberId));
        }

        if (previousStaffMemberId == newStaffMemberId)
        {
            throw new ArgumentException(
                "The new assignee must differ from the previous assignee.",
                nameof(newStaffMemberId));
        }

        return new CaseAssignmentHistory(
            caseId,
            organizationId,
            previousStaffMemberId,
            newStaffMemberId,
            assignedAt,
            assignedByUserId);
    }
}
