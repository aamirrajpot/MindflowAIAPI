using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class WellnessDynamicQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CopingMechanisms",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "JoyPeaceSources",
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

            migrationBuilder.AddColumn<string>(
                name: "Questions",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmotionTag",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifeArea",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceBrainDumpEntryId",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTextExcerpt",
                schema: "app",
                table: "Tasks",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Questions",
                schema: "app",
                table: "WellnessCheckIns");

            migrationBuilder.DropColumn(
                name: "EmotionTag",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "LifeArea",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SourceBrainDumpEntryId",
                schema: "app",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SourceTextExcerpt",
                schema: "app",
                table: "Tasks");

            migrationBuilder.AddColumn<string>(
                name: "CopingMechanisms",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JoyPeaceSources",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfCareFrequency",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StressNotes",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportAreas",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThoughtTrackingMethod",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToughDayMessage",
                schema: "app",
                table: "WellnessCheckIns",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }
    }
}
