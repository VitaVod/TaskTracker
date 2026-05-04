using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationCredentialsStory71 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntegrationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntegrationName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SecretSalt = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RotatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationCredentials_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationCredentialScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCredentialScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationCredentialScopes_IntegrationCredentials_CredentialId",
                        column: x => x.CredentialId,
                        principalTable: "IntegrationCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_KeyId",
                table: "IntegrationCredentials",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_OwnerUserId_Status_CreatedAtUtc",
                table: "IntegrationCredentials",
                columns: new[] { "OwnerUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_Status_ExpiresAtUtc",
                table: "IntegrationCredentials",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentialScopes_CredentialId_Scope",
                table: "IntegrationCredentialScopes",
                columns: new[] { "CredentialId", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentialScopes_Scope",
                table: "IntegrationCredentialScopes",
                column: "Scope");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationCredentialScopes");

            migrationBuilder.DropTable(
                name: "IntegrationCredentials");
        }
    }
}
