namespace PublicServiceCaseManagement.Api.Domain.Audit;

/// <summary>
/// The area of the product an audit event belongs to. Categories are stored
/// as text so the trail stays readable in the database and can be filtered
/// without a second lookup table.
/// </summary>
public enum AuditCategory
{
    Authentication,
    Account,
    Organization,
    Membership,
    Staff,
    Case,
    AccessToken
}
