using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCitizenProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "citizen_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NIDNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CitizenshipNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Age = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PermanentAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TemporaryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_citizen_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_citizen_profiles_application_users_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_citizen_profiles_ApplicationUserId",
                table: "citizen_profiles",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_citizen_profiles_CitizenshipNumber",
                table: "citizen_profiles",
                column: "CitizenshipNumber",
                unique: true,
                filter: "\"CitizenshipNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_profiles_Email",
                table: "citizen_profiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_profiles_NIDNumber",
                table: "citizen_profiles",
                column: "NIDNumber",
                unique: true,
                filter: "\"NIDNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "citizen_profiles");
        }
    }
}
