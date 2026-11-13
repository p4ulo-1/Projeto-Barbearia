using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiSiteChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSiteChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Request = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSiteChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiSiteChangePages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageSlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChangeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSiteChangePages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSiteChangePages_AiSiteChanges_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "AiSiteChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiSiteChangeFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrentValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProposedValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PageId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSiteChangeFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSiteChangeFields_AiSiteChangePages_PageId",
                        column: x => x.PageId,
                        principalTable: "AiSiteChangePages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiSiteChangeFields_PageId",
                table: "AiSiteChangeFields",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSiteChangePages_ChangeId",
                table: "AiSiteChangePages",
                column: "ChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSiteChanges_TenantId_Status",
                table: "AiSiteChanges",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSiteChangeFields");

            migrationBuilder.DropTable(
                name: "AiSiteChangePages");

            migrationBuilder.DropTable(
                name: "AiSiteChanges");
        }
    }
}
