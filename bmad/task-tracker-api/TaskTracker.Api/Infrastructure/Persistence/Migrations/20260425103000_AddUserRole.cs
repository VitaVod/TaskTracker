using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskTracker.Api.Infrastructure.Persistence;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(TaskTrackerDbContext))]
    [Migration("20260425103000_AddUserRole")]
    public partial class AddUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "User");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }
    }
}
