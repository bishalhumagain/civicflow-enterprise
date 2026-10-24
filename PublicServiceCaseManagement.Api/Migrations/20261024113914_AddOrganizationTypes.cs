using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationTypeId",
                table: "organizations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "organization_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_types", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "organization_types",
                columns: new[] { "Id", "Code", "CreatedAt", "IsActive", "Name", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "UNCATEGORIZED", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Uncategorized", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "organizations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "OrganizationTypeId",
                value: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_organizations_OrganizationTypeId",
                table: "organizations",
                column: "OrganizationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_types_Code",
                table: "organization_types",
                column: "Code",
                unique: true);

            // Any organization that existed before this migration has the
            // Guid.Empty default value written by AddColumn above. Point them
            // at the seeded Uncategorized row so the foreign key we are about
            // to add does not fail on existing data.
            migrationBuilder.Sql(
                "UPDATE organizations " +
                "SET \"OrganizationTypeId\" = '00000000-0000-0000-0000-000000000001'::uuid " +
                "WHERE \"OrganizationTypeId\" = '00000000-0000-0000-0000-000000000000'::uuid;");

            migrationBuilder.AddForeignKey(
                name: "FK_organizations_organization_types_OrganizationTypeId",
                table: "organizations",
                column: "OrganizationTypeId",
                principalTable: "organization_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_organizations_organization_types_OrganizationTypeId",
                table: "organizations");

            migrationBuilder.DropTable(
                name: "organization_types");

            migrationBuilder.DropIndex(
                name: "IX_organizations_OrganizationTypeId",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "OrganizationTypeId",
                table: "organizations");
        }
    }
}
