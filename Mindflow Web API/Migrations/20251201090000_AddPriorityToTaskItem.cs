using Microsoft.EntityFrameworkCore.Migrations;

namespace Mindflow_Web_API.Migrations
{
    public partial class AddPriorityToTaskItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Urgency",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Urgency",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Importance",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "PriorityScore",
                schema: "app",
                table: "Tasks");
        }
    }
}


