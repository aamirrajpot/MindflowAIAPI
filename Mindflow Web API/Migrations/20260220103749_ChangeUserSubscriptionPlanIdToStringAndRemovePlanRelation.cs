using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mindflow_Web_API.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserSubscriptionPlanIdToStringAndRemovePlanRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_PlanId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_ProductId",
                schema: "app",
                table: "UserSubscriptions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_Provider_OriginalTransactionId",
                schema: "app",
                table: "UserSubscriptions",
                columns: new[] { "Provider", "OriginalTransactionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_ProductId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_Provider_OriginalTransactionId",
                schema: "app",
                table: "UserSubscriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_PlanId",
                schema: "app",
                table: "UserSubscriptions",
                column: "PlanId",
                principalSchema: "app",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
