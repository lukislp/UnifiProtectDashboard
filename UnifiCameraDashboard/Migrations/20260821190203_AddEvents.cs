using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UnifiCameraDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnifiEventId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CameraUnifiId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SmartDetectTypes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    End = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CameraUnifiId",
                table: "Events",
                column: "CameraUnifiId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Start",
                table: "Events",
                column: "Start");

            migrationBuilder.CreateIndex(
                name: "IX_Events_UnifiEventId",
                table: "Events",
                column: "UnifiEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
