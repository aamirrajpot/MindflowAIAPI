using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class WellnessCheckIn_FreeTime_Reminder_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReminderEnabled",
                schema: "app",
                table: "WellnessCheckIns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReminderTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekdayFreeTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendFreeTime",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderEnabled",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "ReminderTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekdayFreeTime",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendFreeTime",
                schema: "app",
                table: "WellnessCheckIns");
        }
    }
}
