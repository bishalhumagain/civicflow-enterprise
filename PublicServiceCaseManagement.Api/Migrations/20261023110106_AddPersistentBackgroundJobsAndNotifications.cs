using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublicServiceCaseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentBackgroundJobsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "background_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "background_job_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_job_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_background_job_attempts_background_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "background_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryChannel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DeliveryStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BackgroundJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeliveryErrorDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_application_users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_background_jobs_BackgroundJobId",
                        column: x => x.BackgroundJobId,
                        principalTable: "background_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_background_job_attempts_JobId_AttemptNumber",
                table: "background_job_attempts",
                columns: new[] { "JobId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_CorrelationId",
                table: "background_jobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_IdempotencyKey",
                table: "background_jobs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_LeaseExpiresAt",
                table: "background_jobs",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_Status_ScheduledAt",
                table: "background_jobs",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_Type_CreatedAt",
                table: "background_jobs",
                columns: new[] { "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_BackgroundJobId",
                table: "notifications",
                column: "BackgroundJobId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_ExpiresAt",
                table: "notifications",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_IdempotencyKey",
                table: "notifications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RecipientUserId_IsRead_CreatedAt",
                table: "notifications",
                columns: new[] { "RecipientUserId", "IsRead", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_job_attempts");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "background_jobs");
        }
    }
}
