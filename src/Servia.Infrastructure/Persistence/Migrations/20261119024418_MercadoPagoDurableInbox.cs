using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MercadoPagoDurableInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ChangedBy",
                table: "PlanChanges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "MercadoPagoInbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SignatureTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MercadoPagoInbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MercadoPagoInbox_AvailableAt_ReceivedAt",
                table: "MercadoPagoInbox",
                columns: new[] { "AvailableAt", "ReceivedAt" },
                filter: "\"CompletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MercadoPagoInbox_RequestId_ResourceId_SignatureTimestamp",
                table: "MercadoPagoInbox",
                columns: new[] { "RequestId", "ResourceId", "SignatureTimestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MercadoPagoInbox_Topic_ExternalId_ResourceId",
                table: "MercadoPagoInbox",
                columns: new[] { "Topic", "ExternalId", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MercadoPagoInbox");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChangedBy",
                table: "PlanChanges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
