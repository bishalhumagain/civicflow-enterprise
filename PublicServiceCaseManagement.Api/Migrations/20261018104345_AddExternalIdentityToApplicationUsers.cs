using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdentityToApplicationUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalIdentityIssuer",
                table: "application_users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalIdentitySubject",
                table: "application_users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_users_ExternalIdentityIssuer_ExternalIdentitySu~",
                table: "application_users",
                columns: new[] { "ExternalIdentityIssuer", "ExternalIdentitySubject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_application_users_ExternalIdentityIssuer_ExternalIdentitySu~",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "ExternalIdentityIssuer",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "ExternalIdentitySubject",
                table: "application_users");
        }
    }
}
