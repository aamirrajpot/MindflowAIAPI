using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AddBrainDumpEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrainDumpEntries",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                    Context = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Mood = table.Column<int>(type: "INTEGER", nullable: true),
                    Stress = table.Column<int>(type: "INTEGER", nullable: true),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    PromptHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TokensEstimate = table.Column<int>(type: "INTEGER", nullable: true),
                    SuggestionsPreview = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsFlagged = table.Column<bool>(type: "INTEGER", nullable: false),
                    FlagReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainDumpEntries", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrainDumpEntries",
                schema: "app");
        }
    }
}
