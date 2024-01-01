using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SarsMinimalApi.Migrations
{
    /// <inheritdoc />
    public partial class AppsMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Apps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AndroidVersion = table.Column<int>(type: "int", nullable: false),
                    IOSVersion = table.Column<int>(type: "int", nullable: false),
                    WindowsVersion = table.Column<int>(type: "int", nullable: false),
                    MacosVersion = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apps", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Apps");
        }
    }
}
