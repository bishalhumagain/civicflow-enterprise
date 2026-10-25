using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Platform;

/// <summary>
/// A case as seen in the platform-wide case directory. It carries the
/// organization and the people involved so a platform administrator can work
/// without switching into that organization.
/// </summary>
public sealed record PlatformCaseSummary(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    CaseStatus Status,
    CasePriority Priority,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationCode,
    Guid? AssignedToStaffMemberId,
    string? AssignedToName,
    CaseAssignmentRole? AssignedRole,
    Guid? SubmittedByUserId,
    string? SubmittedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PlatformCasePageResponse(
    IReadOnlyList<PlatformCaseSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record PlatformCasePerson(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber);

public sealed record PlatformCaseStaffPerson(
    Guid StaffMemberId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsActive);

public sealed record PlatformCaseStatusEntry(
    Guid Id,
    CaseStatus PreviousStatus,
    CaseStatus NewStatus,
    DateTimeOffset ChangedAt,
    Guid? ChangedByUserId,
    string? ChangedByEmail,
    string? Explanation,
    bool IsVisibleToCitizen);

public sealed record PlatformCaseUpdateEntry(
    Guid Id,
    CaseUpdateKind Kind,
    string Message,
    bool IsVisibleToCitizen,
    Guid AuthorUserId,
    string? AuthorEmail,
    DateTimeOffset CreatedAt);

public sealed record PlatformCaseAssignmentEntry(
    Guid Id,
    Guid? PreviousStaffMemberId,
    string? PreviousStaffMemberName,
    Guid NewStaffMemberId,
    string? NewStaffMemberName,
    CaseAssignmentRole? PreviousRole,
    CaseAssignmentRole NewRole,
    DateTimeOffset AssignedAt,
    Guid? AssignedByUserId,
    string? AssignedByEmail);

public sealed record PlatformCaseDocumentEntry(
    Guid Id,
    Guid CaseId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    DateTimeOffset UploadedAt,
    Guid? UploadedByUserId,
    string? UploadedByEmail,
    bool IsVisibleToCitizen);

public sealed record PlatformCaseDetailResponse(
    PlatformCaseSummary Case,
    PlatformCasePerson? SubmittedBy,
    PlatformCaseStaffPerson? AssignedTo,
    IReadOnlyList<PlatformCaseStatusEntry> StatusHistory,
    IReadOnlyList<PlatformCaseUpdateEntry> Updates,
    IReadOnlyList<PlatformCaseAssignmentEntry> AssignmentHistory,
    IReadOnlyList<PlatformCaseDocumentEntry> Documents);
