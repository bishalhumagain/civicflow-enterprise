using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations;

[Migration("20261019130000_AddAssignmentAuditAttribution")]
[DbContext(typeof(CivicFlowDbContext))]
public partial class AddAssignmentAuditAttribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AssignedByUserId",
            table: "case_assignment_history",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AssignedByUserId",
            table: "case_assignment_history");
    }
}
