using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SarsMinimalApi.Migrations
{
    /// <inheritdoc />
    public partial class TokenMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppId",
                table: "Tokens",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppId",
                table: "Tokens");
        }
    }
}
