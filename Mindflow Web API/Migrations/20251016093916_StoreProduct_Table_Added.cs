using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class StoreProduct_Table_Added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppAccountToken",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenewEnabled",
                schema: "app",
                table: "UserSubscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleObfuscatedAccountId",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleObfuscatedProfileId",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestTransactionId",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalTransactionId",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductId",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                schema: "app",
                table: "UserSubscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawRenewalPayload",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawTransactionPayload",
                schema: "app",
                table: "UserSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoreProducts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProducts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreProducts",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "AppAccountToken",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "AutoRenewEnabled",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "Environment",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "GoogleObfuscatedAccountId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "GoogleObfuscatedProfileId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "LatestTransactionId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "OriginalTransactionId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProductId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "RawRenewalPayload",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "RawTransactionPayload",
                schema: "app",
                table: "UserSubscriptions");
        }
    }
}
