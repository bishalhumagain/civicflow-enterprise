using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations;

/// <summary>
/// Grants a read-only cross-tenant view to a request that has verified a
/// platform super administrator. The new policies are SELECT-only: the
/// existing all-command tenant policies still govern INSERT, UPDATE and
/// DELETE, so the platform flag cannot become a write path.
/// </summary>
[Migration("20261025150000_AddPlatformReadRls")]
[DbContext(typeof(CivicFlowDbContext))]
public sealed class AddPlatformReadRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantTables)
        {
            migrationBuilder.Sql(
                $"""
                 create policy {PolicyName(table)} on "{table}"
                 for select
                 using (
                     current_setting('civicflow.platform_read', true) = 'on'
                 );
                 """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantTables)
        {
            migrationBuilder.Sql(
                $"drop policy if exists {PolicyName(table)} on \"{table}\";");
        }
    }

    private static string PolicyName(string table) =>
        $"{table}_platform_read";

    private static readonly string[] TenantTables =
    [
        "cases",
        "case_status_history",
        "case_assignment_history",
        "case_documents",
        "case_updates",
        "staff_members"
    ];
}
