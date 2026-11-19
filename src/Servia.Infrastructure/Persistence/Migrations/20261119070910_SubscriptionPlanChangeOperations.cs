using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionPlanChangeOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginalCheckoutPlanId",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalAmount",
                table: "PlanChanges",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalPaymentId",
                table: "PlanChanges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CycleEnd",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CycleStart",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Issue",
                table: "PlanChanges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperationKey",
                table: "PlanChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentApprovedAt",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentDispatchedAt",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecurrenceConfirmedAt",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecurrenceDispatchedAt",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourcePrice",
                table: "PlanChanges",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PlanChanges",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_PlanChanges_AdditionalPaymentId",
                table: "PlanChanges",
                column: "AdditionalPaymentId",
                unique: true,
                filter: "\"AdditionalPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlanChanges_TenantId_OperationKey",
                table: "PlanChanges",
                columns: new[] { "TenantId", "OperationKey" },
                unique: true,
                filter: "\"OperationKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanChanges_AdditionalPaymentId",
                table: "PlanChanges");

            migrationBuilder.DropIndex(
                name: "IX_PlanChanges_TenantId_OperationKey",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "OriginalCheckoutPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "AdditionalAmount",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "AdditionalPaymentId",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "CycleEnd",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "CycleStart",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "Issue",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "PaymentApprovedAt",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "PaymentDispatchedAt",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "RecurrenceConfirmedAt",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "RecurrenceDispatchedAt",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "SourcePrice",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PlanChanges");
        }
    }
}
