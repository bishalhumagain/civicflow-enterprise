using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations;

[Migration("20261019150000_AddTenantRowLevelSecurity")]
[DbContext(typeof(CivicFlowDbContext))]
public partial class AddTenantRowLevelSecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantTables)
        {
            migrationBuilder.Sql($"alter table \"{table}\" enable row level security;");
            migrationBuilder.Sql($"alter table \"{table}\" force row level security;");
            migrationBuilder.Sql($"create policy {PolicyName(table)} on \"{table}\" using (current_setting('civicflow.organization_id', true) = \"OrganizationId\"::text) with check (current_setting('civicflow.organization_id', true) = \"OrganizationId\"::text);");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantTables)
        {
            migrationBuilder.Sql($"drop policy if exists {PolicyName(table)} on \"{table}\";");
            migrationBuilder.Sql($"alter table \"{table}\" no force row level security;");
            migrationBuilder.Sql($"alter table \"{table}\" disable row level security;");
        }
    }

    private static string PolicyName(string table) =>
        $"{table}_organization_isolation";

    private static readonly string[] TenantTables =
    [
        "cases",
        "case_status_history",
        "case_assignment_history",
        "case_documents",
        "staff_members"
    ];
}
