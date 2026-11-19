using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionCheckoutFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Subscriptions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<int>(
                name: "BillingIntervalMonths",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancellationEffectiveOn",
                table: "Subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancellationRequestedOn",
                table: "Subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CheckoutFailedOn",
                table: "Subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutKey",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractCurrency",
                table: "Subscriptions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractPriceAmount",
                table: "Subscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstPaymentId",
                table: "Subscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PastDueSince",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveOn",
                table: "PlanChanges",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PlanChanges",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Effective");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_FirstPaymentId",
                table: "Subscriptions",
                column: "FirstPaymentId",
                unique: true,
                filter: "\"FirstPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_GatewaySubscriptionId",
                table: "Subscriptions",
                column: "GatewaySubscriptionId",
                unique: true,
                filter: "\"GatewaySubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_CheckoutKey",
                table: "Subscriptions",
                columns: new[] { "TenantId", "CheckoutKey" },
                unique: true,
                filter: "\"CheckoutKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_EffectiveTenant",
                table: "Subscriptions",
                column: "TenantId",
                unique: true,
                filter: "\"Status\" IN ('Trialing', 'Active') OR (\"Status\" = 'PastDue' AND \"TrialEndsAt\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_PendingTenant",
                table: "Subscriptions",
                column: "TenantId",
                unique: true,
                filter: "\"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_CheckoutState",
                table: "Subscriptions",
                sql: "\"CheckoutKey\" IS NULL OR (\"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment', 'Cancelled', 'Expired') OR (\"Status\" IN ('Active', 'PastDue') AND \"FirstPaymentId\" IS NOT NULL AND \"GatewaySubscriptionId\" IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_FirstPaymentId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_GatewaySubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId_CheckoutKey",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_EffectiveTenant",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_PendingTenant",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_CheckoutState",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingIntervalMonths",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CancellationEffectiveOn",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedOn",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CheckoutFailedOn",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CheckoutKey",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ContractCurrency",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ContractPriceAmount",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "FirstPaymentId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PastDueSince",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "EffectiveOn",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PlanChanges");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Subscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
