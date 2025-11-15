using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class Time_With_Slots_Added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WeekdayEndTimeUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekdayStartTimeUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendEndTimeUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekendStartTimeUtc",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekdayEndTimeUtc",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekdayStartTimeUtc",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendEndTimeUtc",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "WeekendStartTimeUtc",
                schema: "app",
                table: "WellnessCheckIns");
        }
    }
}
