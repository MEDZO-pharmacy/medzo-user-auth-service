using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medzo.Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSequentialUserNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserNumber",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserNumber",
                table: "Users",
                column: "UserNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_UserNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserNumber",
                table: "Users");
        }
    }
}
