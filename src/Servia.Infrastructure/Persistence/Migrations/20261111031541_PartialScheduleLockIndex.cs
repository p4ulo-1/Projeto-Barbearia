using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartialScheduleLockIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleLocks_TenantId_ProfessionalId_Date_StartTime",
                table: "ScheduleLocks");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ScheduleLocks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleLocks_TenantId_ProfessionalId_Date_StartTime",
                table: "ScheduleLocks",
                columns: new[] { "TenantId", "ProfessionalId", "Date", "StartTime" },
                unique: true,
                filter: "\"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleLocks_TenantId_ProfessionalId_Date_StartTime",
                table: "ScheduleLocks");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ScheduleLocks");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleLocks_TenantId_ProfessionalId_Date_StartTime",
                table: "ScheduleLocks",
                columns: new[] { "TenantId", "ProfessionalId", "Date", "StartTime" },
                unique: true,
                filter: "\"ExpiresAt\" > now()");
        }
    }
}
