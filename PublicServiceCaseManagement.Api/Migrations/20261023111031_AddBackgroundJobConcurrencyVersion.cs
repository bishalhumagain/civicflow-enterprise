using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobConcurrencyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "background_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "background_jobs");
        }
    }
}
