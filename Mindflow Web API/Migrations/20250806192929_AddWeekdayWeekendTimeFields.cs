using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekdayWeekendTimeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekdayFreeTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendFreeTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.AddColumn<string>(
                name: "WeekdayEndShift",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekdayEndTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekdayStartShift",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekdayStartTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendEndShift",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendEndTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendStartShift",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendStartTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekdayEndShift",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekdayEndTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekdayStartShift",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekdayStartTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendEndShift",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendEndTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendStartShift",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendStartTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.AddColumn<string>(
                name: "WeekdayFreeTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendFreeTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
