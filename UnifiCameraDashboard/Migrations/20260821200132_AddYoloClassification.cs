using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UnifiCameraDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddYoloClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "YoloClassifiedAt",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YoloLabels",
                table: "Events",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YoloClassifiedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "YoloLabels",
                table: "Events");
        }
    }
}
