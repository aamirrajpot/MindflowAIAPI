using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAndPaymentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentCards",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CardholderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiryMonth = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExpiryYear = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CardType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastFourDigits = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentCards_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionFeatures",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BillingCycle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    OriginalPrice = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPopular = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentHistory",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentHistory_PaymentCards_PaymentCardId",
                        column: x => x.PaymentCardId,
                        principalSchema: "app",
                        principalTable: "PaymentCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentHistory_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "app",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentHistory_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatures",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsIncluded = table.Column<bool>(type: "bit", nullable: false),
                    Limit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanFeatures_SubscriptionFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalSchema: "app",
                        principalTable: "SubscriptionFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanFeatures_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "app",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanFeatures_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "app",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "app",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCards_IsActive",
                schema: "app",
                table: "PaymentCards",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCards_IsDefault",
                schema: "app",
                table: "PaymentCards",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCards_UserId",
                schema: "app",
                table: "PaymentCards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_PaymentCardId",
                schema: "app",
                table: "PaymentHistory",
                column: "PaymentCardId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_Status",
                schema: "app",
                table: "PaymentHistory",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_SubscriptionPlanId",
                schema: "app",
                table: "PaymentHistory",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_TransactionDate",
                schema: "app",
                table: "PaymentHistory",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_TransactionId",
                schema: "app",
                table: "PaymentHistory",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_UserId",
                schema: "app",
                table: "PaymentHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_FeatureId",
                schema: "app",
                table: "PlanFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_IsIncluded",
                schema: "app",
                table: "PlanFeatures",
                column: "IsIncluded");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_PlanId",
                schema: "app",
                table: "PlanFeatures",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_PlanId_FeatureId",
                schema: "app",
                table: "PlanFeatures",
                columns: new[] { "PlanId", "FeatureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_SubscriptionPlanId",
                schema: "app",
                table: "PlanFeatures",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFeatures_IsActive",
                schema: "app",
                table: "SubscriptionFeatures",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFeatures_Name",
                schema: "app",
                table: "SubscriptionFeatures",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFeatures_SortOrder",
                schema: "app",
                table: "SubscriptionFeatures",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_IsActive",
                schema: "app",
                table: "SubscriptionPlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Name",
                schema: "app",
                table: "SubscriptionPlans",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_SortOrder",
                schema: "app",
                table: "SubscriptionPlans",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_EndDate",
                schema: "app",
                table: "UserSubscriptions",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PlanId",
                schema: "app",
                table: "UserSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_StartDate",
                schema: "app",
                table: "UserSubscriptions",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_Status",
                schema: "app",
                table: "UserSubscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId",
                schema: "app",
                table: "UserSubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentHistory",
                schema: "app");

            migrationBuilder.DropTable(
                name: "PlanFeatures",
                schema: "app");

            migrationBuilder.DropTable(
                name: "UserSubscriptions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "PaymentCards",
                schema: "app");

            migrationBuilder.DropTable(
                name: "SubscriptionFeatures",
                schema: "app");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans",
                schema: "app");
        }
    }
}
