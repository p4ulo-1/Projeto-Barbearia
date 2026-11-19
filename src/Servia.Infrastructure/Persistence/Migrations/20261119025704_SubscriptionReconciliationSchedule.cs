using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionReconciliationSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutPayerEmail",
                table: "Subscriptions",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionReconciliation",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextCheckAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RateLimitedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NeedsIntervention = table.Column<bool>(type: "boolean", nullable: false),
                    Result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionReconciliation", x => x.SubscriptionId);
                    table.ForeignKey(
                        name: "FK_SubscriptionReconciliation_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Reconciliation",
                table: "Subscriptions",
                columns: new[] { "CheckoutDispatchedAt", "Id" },
                filter: "\"CheckoutDispatchedAt\" IS NOT NULL AND \"CheckoutKey\" IS NOT NULL AND \"CancellationEffectiveOn\" IS NULL AND \"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment', 'Trialing', 'Active', 'PastDue')");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReconciliation_NextCheckAt",
                table: "SubscriptionReconciliation",
                column: "NextCheckAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReconciliation_RateLimitedUntil",
                table: "SubscriptionReconciliation",
                column: "RateLimitedUntil",
                filter: "\"RateLimitedUntil\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionReconciliation");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_Reconciliation",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CheckoutPayerEmail",
                table: "Subscriptions");
        }
    }
}
