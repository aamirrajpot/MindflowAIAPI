using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskItemStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "app",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                schema: "app",
                table: "Tasks");
        }
    }
}
