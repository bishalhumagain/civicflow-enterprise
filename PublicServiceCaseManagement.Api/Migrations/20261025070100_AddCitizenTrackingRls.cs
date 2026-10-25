using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations;

/// <summary>
/// Extends tenant row-level security to the citizen tracking surface:
/// activity entries are readable by their owner only when citizen-visible,
/// status history writes from a citizen context are limited to the citizen's
/// own case, and visible documents can be read by their owner.
/// </summary>
[Migration("20261025070100_AddCitizenTrackingRls")]
[DbContext(typeof(CivicFlowDbContext))]
public sealed class AddCitizenTrackingRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            alter table "case_updates" enable row level security;
            alter table "case_updates" force row level security;

            create policy case_updates_organization_isolation
            on "case_updates"
            using (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or (
                    current_setting('civicflow.user_id', true) is not null
                    and "IsVisibleToCitizen"
                    and exists (
                        select 1
                        from "cases" citizen_case
                        where citizen_case."Id" = "case_updates"."CaseId"
                          and citizen_case."SubmittedByUserId"::text =
                              current_setting('civicflow.user_id', true)
                    )
                )
            )
            with check (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or (
                    current_setting('civicflow.user_id', true) is not null
                    and "IsVisibleToCitizen"
                    and "AuthorUserId"::text =
                        current_setting('civicflow.user_id', true)
                    and exists (
                        select 1
                        from "cases" citizen_case
                        where citizen_case."Id" = "case_updates"."CaseId"
                          and citizen_case."SubmittedByUserId"::text =
                              current_setting('civicflow.user_id', true)
                    )
                )
            );
            """);

        // Citizen responses insert timeline rows in the citizen transaction
        // context, so the write check must allow the owner's own case.
        migrationBuilder.Sql(
            """
            alter policy case_status_history_organization_isolation
            on "case_status_history"
            using (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or exists (
                    select 1
                    from "cases" citizen_case
                    where citizen_case."Id" = "case_status_history"."CaseId"
                      and citizen_case."OrganizationId" = "case_status_history"."OrganizationId"
                      and citizen_case."SubmittedByUserId"::text =
                          current_setting('civicflow.user_id', true)
                )
            )
            with check (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or exists (
                    select 1
                    from "cases" citizen_case
                    where citizen_case."Id" = "case_status_history"."CaseId"
                      and citizen_case."OrganizationId" = "case_status_history"."OrganizationId"
                      and citizen_case."SubmittedByUserId"::text =
                          current_setting('civicflow.user_id', true)
                )
            );
            """);

        migrationBuilder.Sql(
            """
            alter policy case_documents_organization_isolation
            on "case_documents"
            using (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or (
                    current_setting('civicflow.user_id', true) is not null
                    and "IsVisibleToCitizen"
                    and exists (
                        select 1
                        from "cases" citizen_case
                        where citizen_case."Id" = "case_documents"."CaseId"
                          and citizen_case."OrganizationId" = "case_documents"."OrganizationId"
                          and citizen_case."SubmittedByUserId"::text =
                              current_setting('civicflow.user_id', true)
                    )
                )
            )
            with check (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or (
                    current_setting('civicflow.user_id', true) is not null
                    and "IsVisibleToCitizen"
                    and exists (
                        select 1
                        from "cases" citizen_case
                        where citizen_case."Id" = "case_documents"."CaseId"
                          and citizen_case."OrganizationId" = "case_documents"."OrganizationId"
                          and citizen_case."SubmittedByUserId"::text =
                              current_setting('civicflow.user_id', true)
                    )
                )
            );
            """);

        // A citizen response notifies the assigned staff member. Resolving the
        // staff row happens after the response was authorized against the
        // citizen's own case, so the policy grants read access to exactly that
        // assigned row and keeps all staff writes organization-only.
        migrationBuilder.Sql(
            """
            alter policy staff_members_organization_isolation
            on "staff_members"
            using (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or (
                    current_setting('civicflow.user_id', true) is not null
                    and exists (
                        select 1
                        from "cases" citizen_case
                        where citizen_case."AssignedToStaffMemberId" = "staff_members"."Id"
                          and citizen_case."OrganizationId" = "staff_members"."OrganizationId"
                          and citizen_case."SubmittedByUserId"::text =
                              current_setting('civicflow.user_id', true)
                    )
                )
            )
            with check (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            drop policy if exists case_updates_organization_isolation on "case_updates";
            alter table "case_updates" no force row level security;
            alter table "case_updates" disable row level security;
            """);

        migrationBuilder.Sql(
            """
            alter policy case_status_history_organization_isolation
            on "case_status_history"
            using (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or exists (
                    select 1
                    from "cases" citizen_case
                    where citizen_case."Id" = "case_status_history"."CaseId"
                      and citizen_case."OrganizationId" = "case_status_history"."OrganizationId"
                      and citizen_case."SubmittedByUserId"::text =
                          current_setting('civicflow.user_id', true)
                )
            )
            with check (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
            """);

        migrationBuilder.Sql(
            """
            alter policy case_documents_organization_isolation
            on "case_documents"
            using (current_setting('civicflow.organization_id', true) = "OrganizationId"::text)
            with check (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
            """);

        migrationBuilder.Sql(
            """
            alter policy staff_members_organization_isolation
            on "staff_members"
            using (current_setting('civicflow.organization_id', true) = "OrganizationId"::text)
            with check (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
            """);
    }
}
