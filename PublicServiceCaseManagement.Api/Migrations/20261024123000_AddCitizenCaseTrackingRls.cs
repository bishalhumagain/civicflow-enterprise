using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations;

[Migration("20261024123000_AddCitizenCaseTrackingRls")]
[DbContext(typeof(CivicFlowDbContext))]
public sealed class AddCitizenCaseTrackingRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            alter policy cases_organization_isolation
            on "cases"
            using (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or (
                    current_setting('civicflow.user_id', true) is not null
                    and current_setting('civicflow.user_id', true) = "SubmittedByUserId"::text
                )
            )
            with check (
                current_setting('civicflow.organization_id', true) = "OrganizationId"::text
                or (
                    current_setting('civicflow.user_id', true) is not null
                    and current_setting('civicflow.user_id', true) = "SubmittedByUserId"::text
                )
            );
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
                      and citizen_case."SubmittedByUserId"::text = current_setting('civicflow.user_id', true)
                )
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            alter policy cases_organization_isolation
            on "cases"
            using (current_setting('civicflow.organization_id', true) = "OrganizationId"::text)
            with check (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
            """);

        migrationBuilder.Sql(
            """
            alter policy case_status_history_organization_isolation
            on "case_status_history"
            using (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
            """);
    }
}
