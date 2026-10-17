using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_status_history_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_case_status_history_CaseId",
                table: "case_status_history",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_case_status_history_OrganizationId_CaseId_ChangedAt",
                table: "case_status_history",
                columns: new[] { "OrganizationId", "CaseId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_status_history");
        }
    }
}
