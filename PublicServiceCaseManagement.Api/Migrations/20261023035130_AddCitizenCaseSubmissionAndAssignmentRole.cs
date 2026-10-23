using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCitizenCaseSubmissionAndAssignmentRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedRole",
                table: "cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewRole",
                table: "case_assignment_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CaseWorker");

            migrationBuilder.AddColumn<string>(
                name: "PreviousRole",
                table: "case_assignment_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE cases
                SET "AssignedRole" = 'CaseWorker'
                WHERE "AssignedToStaffMemberId" IS NOT NULL;

                UPDATE case_assignment_history
                SET "PreviousRole" = 'CaseWorker'
                WHERE "PreviousStaffMemberId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedRole",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "NewRole",
                table: "case_assignment_history");

            migrationBuilder.DropColumn(
                name: "PreviousRole",
                table: "case_assignment_history");
        }
    }
}
