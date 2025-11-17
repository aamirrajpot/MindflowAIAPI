using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class TimeZoneId_With_Slots_Added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimezoneId",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimezoneId",
                schema: "app",
                table: "WellnessCheckIns");
        }
    }
}
