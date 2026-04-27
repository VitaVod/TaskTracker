using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountProfileSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "en-US");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Locale",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Users");
        }
    }
}
