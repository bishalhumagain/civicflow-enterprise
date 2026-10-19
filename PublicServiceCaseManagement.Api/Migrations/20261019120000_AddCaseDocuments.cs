using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations;

[Migration("20261019120000_AddCaseDocuments")]
[DbContext(typeof(CivicFlowDbContext))]
public partial class AddCaseDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "case_documents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                OriginalFileName = table.Column<string>(
                    type: "character varying(255)",
                    maxLength: 255,
                    nullable: false),
                StorageKey = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false),
                ContentType = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256Hash = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                UploadedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_case_documents", x => x.Id);
                table.ForeignKey(
                    name: "FK_case_documents_cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_case_documents_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_case_documents_OrganizationId_CaseId_UploadedAt",
            table: "case_documents",
            columns: new[] { "OrganizationId", "CaseId", "UploadedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_case_documents_StorageKey",
            table: "case_documents",
            column: "StorageKey",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "case_documents");
    }
}
