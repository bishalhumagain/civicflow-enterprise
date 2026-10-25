using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CivicFlowDbContext))]
    [Migration("20261025140000_AlignCaseTextMinimums")]
    public partial class AlignCaseTextMinimums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The API, domain, frontend, and documentation all require a
            // minimum title of 3 and a minimum description of 10 characters.
            // The original defensive constraint only rejected empty text, so
            // tighten it to match the authoritative validation layer.
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Title_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Title_Length\" " +
                "CHECK (char_length(btrim(\"Title\")) BETWEEN 3 AND 200);");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Description_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Description_Length\" " +
                "CHECK (char_length(btrim(\"Description\")) BETWEEN 10 AND 5000);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Title_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Title_Length\" " +
                "CHECK (char_length(btrim(\"Title\")) BETWEEN 1 AND 200);");

            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "DROP CONSTRAINT IF EXISTS \"CK_cases_Description_Length\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"cases\" " +
                "ADD CONSTRAINT \"CK_cases_Description_Length\" " +
                "CHECK (char_length(btrim(\"Description\")) BETWEEN 1 AND 5000);");
        }
    }
}
