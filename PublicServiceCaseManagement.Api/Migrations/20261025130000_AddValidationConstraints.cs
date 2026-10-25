using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CivicFlowDbContext))]
    [Migration("20261025130000_AddValidationConstraints")]
    public partial class AddValidationConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "ADD CONSTRAINT \"CK_citizen_profiles_Age_Range\" " +
                "CHECK (\"Age\" IS NULL OR (\"Age\" >= 0 AND \"Age\" <= 150));");

            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "ADD CONSTRAINT \"CK_citizen_profiles_FullName_Length\" " +
                "CHECK (char_length(btrim(\"FullName\")) BETWEEN 1 AND 150);");

            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "ADD CONSTRAINT \"CK_citizen_profiles_Email_Length\" " +
                "CHECK (char_length(btrim(\"Email\")) BETWEEN 3 AND 254);");

            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "ADD CONSTRAINT \"CK_citizen_profiles_Identifiers_Length\" " +
                "CHECK ((\"NIDNumber\" IS NULL OR char_length(btrim(\"NIDNumber\")) BETWEEN 1 AND 50) AND " +
                "(\"CitizenshipNumber\" IS NULL OR char_length(btrim(\"CitizenshipNumber\")) BETWEEN 1 AND 50));");

            migrationBuilder.Sql(
                "ALTER TABLE \"case_documents\" " +
                "ADD CONSTRAINT \"CK_case_documents_SizeBytes_Range\" " +
                "CHECK (\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760);");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Title_Length\" " +
                "CHECK (char_length(btrim(\"Title\")) BETWEEN 1 AND 200);");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Description_Length\" " +
                "CHECK (char_length(btrim(\"Description\")) BETWEEN 1 AND 5000);");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Status_Allowed\" " +
                "CHECK (\"Status\" IN (" +
                "'Submitted', 'UnderReview', 'Assigned', " +
                "'InProgress', 'AwaitingInformation', " +
                "'Resolved', 'Closed', 'Rejected'));");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Priority_Allowed\" " +
                "CHECK (\"Priority\" IN ('Low', 'Normal', 'High', 'Critical'));");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_AssignedRole_Allowed\" " +
                "CHECK (\"AssignedRole\" IS NULL OR " +
                "\"AssignedRole\" IN ('Viewer', 'CaseWorker'));");

            migrationBuilder.Sql(
                "ALTER TABLE \"case_updates\" " +
                "ADD CONSTRAINT \"CK_case_updates_Message_Length\" " +
                "CHECK (char_length(btrim(\"Message\")) BETWEEN 1 AND 4000);");

            migrationBuilder.Sql(
                "ALTER TABLE \"case_updates\" " +
                "ADD CONSTRAINT \"CK_case_updates_Kind_Allowed\" " +
                "CHECK (\"Kind\" IN ('PublicUpdate', 'InformationRequest', " +
                "'InternalNote', 'CitizenResponse'));");

            migrationBuilder.Sql(
                "ALTER TABLE \"case_status_history\" " +
                "ADD CONSTRAINT \"CK_case_status_history_Statuses_Allowed\" " +
                "CHECK (\"PreviousStatus\" IN (" +
                "'Submitted', 'UnderReview', 'Assigned', " +
                "'InProgress', 'AwaitingInformation', " +
                "'Resolved', 'Closed', 'Rejected') AND " +
                "\"NewStatus\" IN (" +
                "'Submitted', 'UnderReview', 'Assigned', " +
                "'InProgress', 'AwaitingInformation', " +
                "'Resolved', 'Closed', 'Rejected'));");

            migrationBuilder.Sql(
                "ALTER TABLE \"case_status_history\" " +
                "ADD CONSTRAINT \"CK_case_status_history_Explanation_Length\" " +
                "CHECK (\"Explanation\" IS NULL OR " +
                "char_length(\"Explanation\") <= 2000);");

            migrationBuilder.Sql(
                "ALTER TABLE \"organizations\" " +
                "ADD CONSTRAINT \"CK_organizations_Name_Length\" " +
                "CHECK (char_length(btrim(\"Name\")) BETWEEN 1 AND 200);");

            migrationBuilder.Sql(
                "ALTER TABLE \"organizations\" " +
                "ADD CONSTRAINT \"CK_organizations_Code_Length\" " +
                "CHECK (char_length(btrim(\"Code\")) BETWEEN 1 AND 50);");

            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "ADD CONSTRAINT \"CK_application_users_FullName_Length\" " +
                "CHECK (char_length(btrim(\"FullName\")) BETWEEN 1 AND 150);");

            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "ADD CONSTRAINT \"CK_application_users_Email_Length\" " +
                "CHECK (\"Email\" IS NULL OR " +
                "char_length(btrim(\"Email\")) BETWEEN 3 AND 254);");

            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "ADD CONSTRAINT \"CK_application_users_Phone_Length\" " +
                "CHECK (\"PhoneNumber\" IS NULL OR " +
                "char_length(btrim(\"PhoneNumber\")) BETWEEN 6 AND 30);");

            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "ADD CONSTRAINT \"CK_application_users_PlatformRole_Allowed\" " +
                "CHECK (\"PlatformRole\" IN ('None', 'SuperAdmin'));");

            migrationBuilder.Sql(
                "ALTER TABLE \"staff_members\" " +
                "ADD CONSTRAINT \"CK_staff_members_FullName_Length\" " +
                "CHECK (char_length(btrim(\"FullName\")) BETWEEN 1 AND 150);");

            migrationBuilder.Sql(
                "ALTER TABLE \"staff_members\" " +
                "ADD CONSTRAINT \"CK_staff_members_Email_Length\" " +
                "CHECK (char_length(btrim(\"Email\")) BETWEEN 3 AND 254);");

            migrationBuilder.Sql(
                "ALTER TABLE \"staff_members\" " +
                "ADD CONSTRAINT \"CK_staff_members_Phone_Length\" " +
                "CHECK (\"PhoneNumber\" IS NULL OR " +
                "char_length(btrim(\"PhoneNumber\")) BETWEEN 6 AND 30);");

            migrationBuilder.Sql(
                "ALTER TABLE \"organization_memberships\" " +
                "ADD CONSTRAINT \"CK_organization_memberships_Role_Allowed\" " +
                "CHECK (\"Role\" IN ('Viewer', 'CaseWorker', 'OrganizationAdmin'));");

            migrationBuilder.Sql(
                "ALTER TABLE \"account_invitations\" " +
                "ADD CONSTRAINT \"CK_account_invitations_FullName_Length\" " +
                "CHECK (char_length(btrim(\"FullName\")) BETWEEN 1 AND 150);");

            migrationBuilder.Sql(
                "ALTER TABLE \"account_invitations\" " +
                "ADD CONSTRAINT \"CK_account_invitations_Email_Length\" " +
                "CHECK (char_length(btrim(\"Email\")) BETWEEN 3 AND 254);");

            migrationBuilder.Sql(
                "ALTER TABLE \"account_invitations\" " +
                "ADD CONSTRAINT \"CK_account_invitations_Phone_Length\" " +
                "CHECK (\"PhoneNumber\" IS NULL OR " +
                "char_length(btrim(\"PhoneNumber\")) BETWEEN 6 AND 30);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"account_invitations\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_account_invitations_Phone_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"account_invitations\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_account_invitations_Email_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"account_invitations\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_account_invitations_FullName_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"organization_memberships\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_organization_memberships_Role_Allowed\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"staff_members\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_staff_members_Phone_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"staff_members\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_staff_members_Email_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"staff_members\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_staff_members_FullName_Length\";");

            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_application_users_PlatformRole_Allowed\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_application_users_Phone_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_application_users_Email_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"application_users\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_application_users_FullName_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"organizations\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_organizations_Code_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"organizations\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_organizations_Name_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"case_status_history\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_case_status_history_Explanation_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"case_status_history\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_case_status_history_Statuses_Allowed\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"case_updates\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_case_updates_Kind_Allowed\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"case_updates\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_case_updates_Message_Length\";");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_AssignedRole_Allowed\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Priority_Allowed\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Status_Allowed\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Description_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Title_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"case_documents\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_case_documents_SizeBytes_Range\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_citizen_profiles_Identifiers_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_citizen_profiles_Email_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_citizen_profiles_FullName_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"citizen_profiles\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_citizen_profiles_Age_Range\";");
        }
    }
}
