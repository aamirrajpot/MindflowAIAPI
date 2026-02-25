using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AppAccountToken_Entity_Added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppleAppAccountTokens",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AppAccountToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppleAppAccountTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppleAppAccountTokens_AppAccountToken",
                schema: "app",
                table: "AppleAppAccountTokens",
                column: "AppAccountToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppleAppAccountTokens_UserId",
                schema: "app",
                table: "AppleAppAccountTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppleAppAccountTokens",
                schema: "app");
        }
    }
}
