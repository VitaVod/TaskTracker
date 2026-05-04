using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivilegedActionAuditsStory65 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuspiciousFlagged",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountNotificationDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    ToEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LastFailureCategory = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountNotificationDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountNotificationDispatches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActionAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReasonText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ConfirmDestructive = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmationToken = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IntentKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationActionAudits_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrivilegedActionAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReasonText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IntentKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivilegedActionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivilegedActionAudits_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountNotificationDispatches_EventKey",
                table: "AccountNotificationDispatches",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountNotificationDispatches_Status_CreatedAtUtc",
                table: "AccountNotificationDispatches",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountNotificationDispatches_UserId_EventType_CreatedAtUtc",
                table: "AccountNotificationDispatches",
                columns: new[] { "UserId", "EventType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActionAudits_CaseId_CreatedAtUtc",
                table: "ModerationActionAudits",
                columns: new[] { "CaseId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActionAudits_IntentKey",
                table: "ModerationActionAudits",
                column: "IntentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActionAudits_TargetUserId_CreatedAtUtc",
                table: "ModerationActionAudits",
                columns: new[] { "TargetUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivilegedActionAudits_ActorUserId_OccurredAtUtc",
                table: "PrivilegedActionAudits",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivilegedActionAudits_IntentKey",
                table: "PrivilegedActionAudits",
                column: "IntentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivilegedActionAudits_OccurredAtUtc",
                table: "PrivilegedActionAudits",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PrivilegedActionAudits_TargetUserId_OccurredAtUtc",
                table: "PrivilegedActionAudits",
                columns: new[] { "TargetUserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountNotificationDispatches");

            migrationBuilder.DropTable(
                name: "ModerationActionAudits");

            migrationBuilder.DropTable(
                name: "PrivilegedActionAudits");

            migrationBuilder.DropColumn(
                name: "IsSuspiciousFlagged",
                table: "Users");
        }
    }
}
