namespace PublicServiceCaseManagement.Api.Features.Dashboard;

/// <summary>
/// Case counts for one organization in the platform-wide overview.
/// </summary>
public sealed record PlatformOrganizationCaseBreakdown(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationCode,
    bool IsActive,
    int TotalCases,
    int OpenCases,
    int ResolvedCases,
    int CriticalPriorityCases,
    int HighPriorityCases);
