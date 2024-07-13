using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SarsMinimalApi.Migrations
{
    /// <inheritdoc />
    public partial class MoreColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Process",
                table: "Logs",
                newName: "Method");

            migrationBuilder.AddColumn<int>(
                name: "AppID",
                table: "Logs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AndroidURI",
                table: "Apps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IOSURI",
                table: "Apps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MacosURI",
                table: "Apps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowsURI",
                table: "Apps",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppID",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "AndroidURI",
                table: "Apps");

            migrationBuilder.DropColumn(
                name: "IOSURI",
                table: "Apps");

            migrationBuilder.DropColumn(
                name: "MacosURI",
                table: "Apps");

            migrationBuilder.DropColumn(
                name: "WindowsURI",
                table: "Apps");

            migrationBuilder.RenameColumn(
                name: "Method",
                table: "Logs",
                newName: "Process");
        }
    }
}
