using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingAndPublicSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiChangeSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Theme_Font = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Theme_Layout = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Theme_Primary = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Theme_Secondary = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChangeSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Segment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CollectedFacts = table.Column<string>(type: "text", nullable: false),
                    GeneratedChangeSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnboardingSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiChangeSetSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ChangeSetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChangeSetSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiChangeSetSections_AiChangeSets_ChangeSetId",
                        column: x => x.ChangeSetId,
                        principalTable: "AiChangeSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OnboardingSessionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingMessages_OnboardingSessions_OnboardingSessionId",
                        column: x => x.OnboardingSessionId,
                        principalTable: "OnboardingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiChangeSetSections_ChangeSetId",
                table: "AiChangeSetSections",
                column: "ChangeSetId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMessages_OnboardingSessionId",
                table: "OnboardingMessages",
                column: "OnboardingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMessages_SentAt",
                table: "OnboardingMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingSessions_TenantId_Status",
                table: "OnboardingSessions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssets_TenantId_OnboardingSessionId",
                table: "SiteAssets",
                columns: new[] { "TenantId", "OnboardingSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiChangeSetSections");

            migrationBuilder.DropTable(
                name: "OnboardingMessages");

            migrationBuilder.DropTable(
                name: "SiteAssets");

            migrationBuilder.DropTable(
                name: "AiChangeSets");

            migrationBuilder.DropTable(
                name: "OnboardingSessions");
        }
    }
}
