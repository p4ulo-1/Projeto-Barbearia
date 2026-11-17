using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiTenantIdentifierAndOperationCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O total historico (UnitsConsumed) e preservado como InputTokens:
            // os registros antigos nao tinham a separacao entrada/saida, e
            // renomear para OutputTokens rotularia o total como saida.
            migrationBuilder.RenameColumn(
                name: "UnitsConsumed",
                table: "AiOperationLogs",
                newName: "InputTokens");

            migrationBuilder.AddColumn<decimal>(
                name: "CostUsd",
                table: "AiOperationLogs",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "AiOperationLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KeyLabel",
                table: "AiOperationLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "AiOperationLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PriceTableVersion",
                table: "AiOperationLogs",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AiTenantIdentifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    KeyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousValue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PreviousKeyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RotatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTenantIdentifiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiTenantIdentifiers_TenantId",
                table: "AiTenantIdentifiers",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiTenantIdentifiers");

            migrationBuilder.DropColumn(
                name: "CostUsd",
                table: "AiOperationLogs");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "AiOperationLogs");

            migrationBuilder.DropColumn(
                name: "KeyLabel",
                table: "AiOperationLogs");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "AiOperationLogs");

            migrationBuilder.DropColumn(
                name: "PriceTableVersion",
                table: "AiOperationLogs");

            migrationBuilder.RenameColumn(
                name: "InputTokens",
                table: "AiOperationLogs",
                newName: "UnitsConsumed");
        }
    }
}
