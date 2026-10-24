namespace PublicServiceCaseManagement.Api.Infrastructure.Audit;

/// <summary>
/// Stable action names for the audit trail. They are part of the API
/// contract: administrators filter the log by these values, so they must not
/// change silently.
/// </summary>
public static class AuditActions
{
    public const string AuthenticationSucceeded = "authentication.succeeded";
    public const string AuthenticationFailed = "authentication.failed";

    public const string AccountRegistered = "account.registered";
    public const string AccountPasswordReset = "account.password_reset";
    public const string AccountPasswordChanged = "account.password_changed";
    public const string AccountPrivilegeChanged = "account.privilege_changed";
    public const string AccountProfileUpdated = "account.profile_updated";

    public const string InvitationCreated = "invitation.created";
    public const string InvitationResent = "invitation.resent";
    public const string InvitationRevoked = "invitation.revoked";
    public const string InvitationAccepted = "invitation.accepted";
    public const string InvitationDeliveryFailed = "invitation.delivery_failed";

    public const string OrganizationCreated = "organization.created";
    public const string OrganizationUpdated = "organization.updated";
    public const string OrganizationActivated = "organization.activated";
    public const string OrganizationDeactivated = "organization.deactivated";
    public const string OrganizationDeleted = "organization.deleted";

    public const string OrganizationTypeCreated = "organization_type.created";
    public const string OrganizationTypeUpdated = "organization_type.updated";
    public const string OrganizationTypeActivated = "organization_type.activated";
    public const string OrganizationTypeDeactivated = "organization_type.deactivated";
    public const string OrganizationTypeDeleted = "organization_type.deleted";

    public const string MembershipCreated = "membership.created";
    public const string MembershipRoleChanged = "membership.role_changed";
    public const string MembershipActivated = "membership.activated";
    public const string MembershipDeactivated = "membership.deactivated";
    public const string MembershipTransferred = "membership.transferred";

    public const string StaffCreated = "staff.created";
    public const string StaffActivated = "staff.activated";
    public const string StaffDeactivated = "staff.deactivated";
    public const string StaffPhoneUpdated = "staff.phone_updated";

    public const string CaseCreated = "case.created";
    public const string CaseUpdated = "case.updated";
    public const string CaseDeleted = "case.deleted";
    public const string CaseStatusChanged = "case.status_changed";
    public const string CaseAssigned = "case.assigned";
    public const string CaseDocumentUploaded = "case.document_uploaded";

    public const string AccessTokenCreated = "access_token.created";
    public const string AccessTokenRevoked = "access_token.revoked";
}