using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordRecoveryUserForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_PasswordRecoveryTokens_Users_UserId",
                table: "PasswordRecoveryTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PasswordRecoveryTokens_Users_UserId",
                table: "PasswordRecoveryTokens");
        }
    }
}
