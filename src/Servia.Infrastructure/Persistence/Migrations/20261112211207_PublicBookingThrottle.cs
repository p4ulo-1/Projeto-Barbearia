using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicBookingThrottle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicBookingAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicBookingAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookingAttempts_TenantId_ClientKey_WindowStart",
                table: "PublicBookingAttempts",
                columns: new[] { "TenantId", "ClientKey", "WindowStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookingAttempts_WindowEnd",
                table: "PublicBookingAttempts",
                column: "WindowEnd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicBookingAttempts");
        }
    }
}
