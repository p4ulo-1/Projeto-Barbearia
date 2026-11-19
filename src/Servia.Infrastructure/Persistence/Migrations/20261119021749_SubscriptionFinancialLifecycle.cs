using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionFinancialLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_CheckoutState",
                table: "Subscriptions");

            migrationBuilder.AddColumn<decimal>(
                name: "RecurringAmount",
                table: "PlatformInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurringCurrency",
                table: "PlatformInvoices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationDispatchedAtUtc",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationConfirmedAtUtc",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayTrialPlanId",
                table: "Subscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextChargeAtUtc",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialEndsAtUtc",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrialPlanId",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialStartedAtUtc",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecurringDueAtUtc",
                table: "PlatformInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecurringFailureAtUtc",
                table: "PlatformInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppliedAtUtc",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveAtUtc",
                table: "PlanChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedCurrency",
                table: "PlanChanges",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedPrice",
                table: "PlanChanges",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_CheckoutState",
                table: "Subscriptions",
                sql: "\"CheckoutKey\" IS NULL OR (\"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment', 'Cancelled', 'Expired') OR (\"Status\" = 'Trialing' AND \"TrialStartedAtUtc\" IS NOT NULL AND \"TrialEndsAtUtc\" IS NOT NULL AND \"GatewayTrialPlanId\" IS NOT NULL AND \"GatewaySubscriptionId\" IS NOT NULL) OR (\"Status\" IN ('Active', 'PastDue') AND \"FirstPaymentId\" IS NOT NULL AND \"GatewaySubscriptionId\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "UX_PlatformInvoices_RecurringCycle",
                table: "PlatformInvoices",
                columns: new[] { "TenantId", "SubscriptionId", "RecurringDueAtUtc" },
                unique: true,
                filter: "\"RecurringDueAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PlatformInvoices_RecurringPayment",
                table: "PlatformInvoices",
                column: "GatewayPaymentId",
                unique: true,
                filter: "\"RecurringDueAtUtc\" IS NOT NULL AND \"GatewayPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PlanChanges_RequestedSubscription",
                table: "PlanChanges",
                columns: new[] { "TenantId", "SubscriptionId" },
                unique: true,
                filter: "\"Status\" = 'Requested'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_CheckoutState",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_PlatformInvoices_RecurringCycle",
                table: "PlatformInvoices");

            migrationBuilder.DropIndex(
                name: "UX_PlatformInvoices_RecurringPayment",
                table: "PlatformInvoices");

            migrationBuilder.DropIndex(
                name: "UX_PlanChanges_RequestedSubscription",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "CancellationDispatchedAtUtc",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "RecurringAmount",
                table: "PlatformInvoices");

            migrationBuilder.DropColumn(
                name: "RecurringCurrency",
                table: "PlatformInvoices");

            migrationBuilder.DropColumn(
                name: "CancellationConfirmedAtUtc",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "GatewayTrialPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "NextChargeAtUtc",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TrialEndsAtUtc",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TrialPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TrialStartedAtUtc",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "RecurringDueAtUtc",
                table: "PlatformInvoices");

            migrationBuilder.DropColumn(
                name: "RecurringFailureAtUtc",
                table: "PlatformInvoices");

            migrationBuilder.DropColumn(
                name: "AppliedAtUtc",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "EffectiveAtUtc",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "RequestedCurrency",
                table: "PlanChanges");

            migrationBuilder.DropColumn(
                name: "RequestedPrice",
                table: "PlanChanges");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_CheckoutState",
                table: "Subscriptions",
                sql: "\"CheckoutKey\" IS NULL OR (\"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment', 'Cancelled', 'Expired') OR (\"Status\" IN ('Active', 'PastDue') AND \"FirstPaymentId\" IS NOT NULL AND \"GatewaySubscriptionId\" IS NOT NULL))");
        }
    }
}
