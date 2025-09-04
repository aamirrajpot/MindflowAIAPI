using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringTaskFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "app",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                schema: "app",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxOccurrences",
                schema: "app",
                table: "Tasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextOccurrence",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTaskId",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "MaxOccurrences",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "NextOccurrence",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                schema: "app",
                table: "Tasks");
        }
    }
}
