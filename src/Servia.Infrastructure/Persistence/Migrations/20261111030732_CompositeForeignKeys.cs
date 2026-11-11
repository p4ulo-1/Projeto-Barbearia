using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompositeForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ServicePackages_TenantId_Id",
                table: "ServicePackages",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MembershipPlans_TenantId_Id",
                table: "MembershipPlans",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Appointments_TenantId_Id",
                table: "Appointments",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_TenantId_CustomerId",
                table: "WaitlistEntries",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_TenantId_ProfessionalId",
                table: "WaitlistEntries",
                columns: new[] { "TenantId", "ProfessionalId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_TenantId_ServiceId",
                table: "WaitlistEntries",
                columns: new[] { "TenantId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReviews_TenantId_CustomerId",
                table: "ServiceReviews",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageUsages_TenantId_PackageId",
                table: "PackageUsages",
                columns: new[] { "TenantId", "PackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_MembershipPlanId",
                table: "Memberships",
                columns: new[] { "TenantId", "MembershipPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_TenantId_CustomerId",
                table: "CustomerPreferences",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEntries_TenantId_AppointmentId",
                table: "CommissionEntries",
                columns: new[] { "TenantId", "AppointmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionEntries_Appointments_TenantId_AppointmentId",
                table: "CommissionEntries",
                columns: new[] { "TenantId", "AppointmentId" },
                principalTable: "Appointments",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionEntries_Professionals_TenantId_ProfessionalId",
                table: "CommissionEntries",
                columns: new[] { "TenantId", "ProfessionalId" },
                principalTable: "Professionals",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPreferences_Customers_TenantId_CustomerId",
                table: "CustomerPreferences",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyAccounts_Customers_TenantId_CustomerId",
                table: "LoyaltyAccounts",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Memberships_Customers_TenantId_CustomerId",
                table: "Memberships",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Memberships_MembershipPlans_TenantId_MembershipPlanId",
                table: "Memberships",
                columns: new[] { "TenantId", "MembershipPlanId" },
                principalTable: "MembershipPlans",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PackageUsages_Customers_TenantId_CustomerId",
                table: "PackageUsages",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PackageUsages_ServicePackages_TenantId_PackageId",
                table: "PackageUsages",
                columns: new[] { "TenantId", "PackageId" },
                principalTable: "ServicePackages",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Appointments_TenantId_AppointmentId",
                table: "Payments",
                columns: new[] { "TenantId", "AppointmentId" },
                principalTable: "Appointments",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfessionalServices_Professionals_TenantId_ProfessionalId",
                table: "ProfessionalServices",
                columns: new[] { "TenantId", "ProfessionalId" },
                principalTable: "Professionals",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfessionalServices_Services_TenantId_ServiceId",
                table: "ProfessionalServices",
                columns: new[] { "TenantId", "ServiceId" },
                principalTable: "Services",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleBlocks_Professionals_TenantId_ProfessionalId",
                table: "ScheduleBlocks",
                columns: new[] { "TenantId", "ProfessionalId" },
                principalTable: "Professionals",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceReviews_Appointments_TenantId_AppointmentId",
                table: "ServiceReviews",
                columns: new[] { "TenantId", "AppointmentId" },
                principalTable: "Appointments",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceReviews_Customers_TenantId_CustomerId",
                table: "ServiceReviews",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WaitlistEntries_Customers_TenantId_CustomerId",
                table: "WaitlistEntries",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WaitlistEntries_Professionals_TenantId_ProfessionalId",
                table: "WaitlistEntries",
                columns: new[] { "TenantId", "ProfessionalId" },
                principalTable: "Professionals",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WaitlistEntries_Services_TenantId_ServiceId",
                table: "WaitlistEntries",
                columns: new[] { "TenantId", "ServiceId" },
                principalTable: "Services",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommissionEntries_Appointments_TenantId_AppointmentId",
                table: "CommissionEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CommissionEntries_Professionals_TenantId_ProfessionalId",
                table: "CommissionEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPreferences_Customers_TenantId_CustomerId",
                table: "CustomerPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyAccounts_Customers_TenantId_CustomerId",
                table: "LoyaltyAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Memberships_Customers_TenantId_CustomerId",
                table: "Memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_Memberships_MembershipPlans_TenantId_MembershipPlanId",
                table: "Memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_PackageUsages_Customers_TenantId_CustomerId",
                table: "PackageUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_PackageUsages_ServicePackages_TenantId_PackageId",
                table: "PackageUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Appointments_TenantId_AppointmentId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfessionalServices_Professionals_TenantId_ProfessionalId",
                table: "ProfessionalServices");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfessionalServices_Services_TenantId_ServiceId",
                table: "ProfessionalServices");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleBlocks_Professionals_TenantId_ProfessionalId",
                table: "ScheduleBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceReviews_Appointments_TenantId_AppointmentId",
                table: "ServiceReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceReviews_Customers_TenantId_CustomerId",
                table: "ServiceReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_WaitlistEntries_Customers_TenantId_CustomerId",
                table: "WaitlistEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_WaitlistEntries_Professionals_TenantId_ProfessionalId",
                table: "WaitlistEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_WaitlistEntries_Services_TenantId_ServiceId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_TenantId_CustomerId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_TenantId_ProfessionalId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_TenantId_ServiceId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_ServiceReviews_TenantId_CustomerId",
                table: "ServiceReviews");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ServicePackages_TenantId_Id",
                table: "ServicePackages");

            migrationBuilder.DropIndex(
                name: "IX_PackageUsages_TenantId_PackageId",
                table: "PackageUsages");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_TenantId_MembershipPlanId",
                table: "Memberships");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MembershipPlans_TenantId_Id",
                table: "MembershipPlans");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPreferences_TenantId_CustomerId",
                table: "CustomerPreferences");

            migrationBuilder.DropIndex(
                name: "IX_CommissionEntries_TenantId_AppointmentId",
                table: "CommissionEntries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Appointments_TenantId_Id",
                table: "Appointments");
        }
    }
}
