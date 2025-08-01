using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class WellnessCheckIn_NewFields_Added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgeRange",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FocusAreas",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfCareFrequency",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StressNotes",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportAreas",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThoughtTrackingMethod",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToughDayMessage",
                schema: "app",
                table: "WellnessCheckIns",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeRange",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "FocusAreas",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "SelfCareFrequency",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "StressNotes",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "SupportAreas",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "ThoughtTrackingMethod",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "ToughDayMessage",
                schema: "app",
                table: "WellnessCheckIns");
        }
    }
}
