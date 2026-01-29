using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class TimeSlot_Minuts_UTC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeekdayEndMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeekdayStartMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeekendEndMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeekendStartMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekdayEndMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekdayStartMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendEndMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendStartMinutesUtc",
                schema: "app",
                table: "WellnessCheckIns");
        }
    }
}
