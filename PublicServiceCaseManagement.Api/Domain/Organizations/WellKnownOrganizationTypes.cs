namespace PublicServiceCaseManagement.Api.Domain.Organizations;

/// <summary>
/// Fixed identifiers for organization types that are seeded by the platform
/// and referenced from application code and tests. Keeping them in one place
/// means the seed data, the domain fallback, and the test fixtures always
/// agree on what the default type is.
/// </summary>
public static class WellKnownOrganizationTypes
{
    /// <summary>
    /// The catch-all type assigned to organizations that have not been given
    /// an explicit type. Seeded by <c>CivicFlowDbContext</c> and used as the
    /// fallback whenever a caller does not supply a type identifier.
    /// </summary>
    public static readonly Guid Uncategorized = Guid.Parse(
        "00000000-0000-0000-0000-000000000001");
}
