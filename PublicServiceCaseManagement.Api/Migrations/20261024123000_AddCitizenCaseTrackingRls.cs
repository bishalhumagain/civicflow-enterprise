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
            create policy cases_citizen_read
            on "cases"
            for select
            using (
                current_setting('civicflow.user_id', true) =
                "SubmittedByUserId"::text
            );
            """);

        migrationBuilder.Sql(
            """
            create policy case_status_history_citizen_read
            on "case_status_history"
            for select
            using (
                exists (
                    select 1
                    from "cases" citizen_case
                    where citizen_case."Id" =
                          "case_status_history"."CaseId"
                      and citizen_case."OrganizationId" =
                          "case_status_history"."OrganizationId"
                      and citizen_case."SubmittedByUserId"::text =
                          current_setting('civicflow.user_id', true)
                )
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "drop policy if exists case_status_history_citizen_read on \"case_status_history\";");
        migrationBuilder.Sql(
            "drop policy if exists cases_citizen_read on \"cases\";");
    }
}
