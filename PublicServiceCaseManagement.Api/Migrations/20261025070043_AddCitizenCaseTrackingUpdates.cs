using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCitizenCaseTrackingUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "case_status_history",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisibleToCitizen",
                table: "case_status_history",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisibleToCitizen",
                table: "case_documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "case_updates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsVisibleToCitizen = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_updates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_updates_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_updates_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_case_updates_CaseId",
                table: "case_updates",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_case_updates_OrganizationId_CaseId_CreatedAt",
                table: "case_updates",
                columns: new[] { "OrganizationId", "CaseId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_updates");

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "case_status_history");

            migrationBuilder.DropColumn(
                name: "IsVisibleToCitizen",
                table: "case_status_history");

            migrationBuilder.DropColumn(
                name: "IsVisibleToCitizen",
                table: "case_documents");
        }
    }
}
