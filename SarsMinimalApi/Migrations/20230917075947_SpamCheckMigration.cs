using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SarsMinimalApi.Migrations
{
    /// <inheritdoc />
    public partial class SpamCheckMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Endpoint",
                table: "IpAdresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Endpoint",
                table: "IpAdresses");
        }
    }
}
