using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiInsight",
                schema: "app",
                table: "BrainDumpEntries",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                schema: "app",
                table: "BrainDumpEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                schema: "app",
                table: "BrainDumpEntries",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "app",
                table: "BrainDumpEntries",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WordCount",
                schema: "app",
                table: "BrainDumpEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiInsight",
                schema: "app",
                table: "BrainDumpEntries");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                schema: "app",
                table: "BrainDumpEntries");

            migrationBuilder.DropColumn(
                name: "Tags",
                schema: "app",
                table: "BrainDumpEntries");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "app",
                table: "BrainDumpEntries");

            migrationBuilder.DropColumn(
                name: "WordCount",
                schema: "app",
                table: "BrainDumpEntries");
        }
    }
}
