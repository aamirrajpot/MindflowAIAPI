using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class TaskUrgency_Importance_Changes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Importance",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityScore",
                schema: "app",
                table: "Tasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubSteps",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Urgency",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Importance",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "PriorityScore",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SubSteps",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Urgency",
                schema: "app",
                table: "Tasks");
        }
    }
}
