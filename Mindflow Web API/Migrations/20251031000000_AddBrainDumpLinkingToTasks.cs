using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AddBrainDumpLinkingToTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceBrainDumpEntryId",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTextExcerpt",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifeArea",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmotionTag",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceBrainDumpEntryId",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SourceTextExcerpt",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "LifeArea",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EmotionTag",
                schema: "app",
                table: "Tasks");
        }
    }
}

