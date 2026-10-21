using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToAspNetCoreIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.RenameIndex(
                name: "IX_application_users_NormalizedEmail",
                table: "application_users",
                newName: "EmailIndex");

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "application_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "application_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "application_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockoutEnabled",
                table: "application_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "application_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUserName",
                table: "application_users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "application_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "application_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhoneNumberConfirmed",
                table: "application_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "application_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "application_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "application_users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "identity_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_user_claims_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_identity_user_logins_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_identity_user_tokens_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_role_claims_identity_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "identity_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_identity_user_roles_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_identity_user_roles_identity_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "identity_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "application_users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_role_claims_RoleId",
                table: "identity_role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "identity_roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_claims_UserId",
                table: "identity_user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_logins_UserId",
                table: "identity_user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_roles_RoleId",
                table: "identity_user_roles",
                column: "RoleId");

            // Backfill existing (previously Keycloak-managed) user rows so they
            // remain valid ASP.NET Core Identity accounts. They keep their
            // Guid primary key, which organization memberships and citizen
            // profiles already reference. No local password exists for them,
            // so PasswordHash stays null and they must use the password
            // recovery flow; they are treated as email-confirmed because
            // Keycloak already verified them.
            migrationBuilder.Sql(
                """
                UPDATE application_users
                SET "UserName" = "Email",
                    "NormalizedUserName" = UPPER("Email"),
                    "NormalizedEmail" = UPPER("Email"),
                    "EmailConfirmed" = TRUE,
                    "LockoutEnabled" = TRUE,
                    "SecurityStamp" = COALESCE("SecurityStamp", gen_random_uuid()::text),
                    "ConcurrencyStamp" = COALESCE("ConcurrencyStamp", gen_random_uuid()::text)
                WHERE "UserName" IS NULL;
                """);

            // Seed the role catalogue used by the authorization policies.
            migrationBuilder.Sql(
                """
                INSERT INTO identity_roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                VALUES
                    ('00000000-0000-0000-0000-0000000000a1', 'citizen', 'CITIZEN', gen_random_uuid()::text),
                    ('00000000-0000-0000-0000-0000000000a2', 'staff', 'STAFF', gen_random_uuid()::text),
                    ('00000000-0000-0000-0000-0000000000a3', 'superadmin', 'SUPERADMIN', gen_random_uuid()::text)
                ON CONFLICT ("NormalizedName") DO NOTHING;
                """);

            // Existing platform administrators become superadmins; everyone
            // else is treated as a citizen until an administrator assigns a
            // staff role. This preserves least privilege.
            migrationBuilder.Sql(
                """
                INSERT INTO identity_user_roles ("UserId", "RoleId")
                SELECT u."Id", r."Id"
                FROM application_users u
                JOIN identity_roles r
                    ON r."NormalizedName" = CASE
                        WHEN u."PlatformRole" = 'SuperAdmin' THEN 'SUPERADMIN'
                        ELSE 'CITIZEN'
                    END
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_role_claims");

            migrationBuilder.DropTable(
                name: "identity_user_claims");

            migrationBuilder.DropTable(
                name: "identity_user_logins");

            migrationBuilder.DropTable(
                name: "identity_user_roles");

            migrationBuilder.DropTable(
                name: "identity_user_tokens");

            migrationBuilder.DropTable(
                name: "identity_roles");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "NormalizedUserName",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "PhoneNumberConfirmed",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "application_users");

            migrationBuilder.RenameIndex(
                name: "EmailIndex",
                table: "application_users",
                newName: "IX_application_users_NormalizedEmail");

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
    }
}
