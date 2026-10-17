using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToStaffMemberId",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "case_assignment_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStaffMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewStaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_assignment_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_assignment_history_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cases_OrganizationId_AssignedToStaffMemberId",
                table: "cases",
                columns: new[] { "OrganizationId", "AssignedToStaffMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_case_assignment_history_CaseId",
                table: "case_assignment_history",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_case_assignment_history_OrganizationId_CaseId_AssignedAt",
                table: "case_assignment_history",
                columns: new[] { "OrganizationId", "CaseId", "AssignedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_assignment_history");

            migrationBuilder.DropIndex(
                name: "IX_cases_OrganizationId_AssignedToStaffMemberId",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "AssignedToStaffMemberId",
                table: "cases");
        }
    }
}
