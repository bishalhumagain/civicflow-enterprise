namespace PublicServiceCaseManagement.Api.Domain.Cases;

/// <summary>
/// The kind of activity entry recorded against a case. Internal notes are
/// staff-only; every other kind is part of the citizen-visible conversation.
/// </summary>
public enum CaseUpdateKind
{
    PublicUpdate = 0,
    InformationRequest = 1,
    InternalNote = 2,
    CitizenResponse = 3
}
