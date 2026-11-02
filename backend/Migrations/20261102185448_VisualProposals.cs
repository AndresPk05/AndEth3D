using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AndEth3D.Backend.Migrations
{
    /// <inheritdoc />
    public partial class VisualProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisualConversationUsages",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ProposalCount = table.Column<int>(type: "integer", nullable: false),
                    ChargedUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    ActiveProposalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualConversationUsages", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_VisualConversationUsages_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisualSettingsRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualSettingsRevisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VisualUserUsages",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReservedAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualUserUsages", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_VisualUserUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisualProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ParentProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManufacturingRevisionId = table.Column<int>(type: "integer", nullable: false),
                    SettingsRevisionId = table.Column<int>(type: "integer", nullable: false),
                    BriefJson = table.Column<string>(type: "text", nullable: false),
                    BriefFingerprint = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ReservedUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    AttemptsJson = table.Column<string>(type: "text", nullable: false),
                    ImageFile = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisualProposals_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisualProposals_ManufacturingRevisions_ManufacturingRevisio~",
                        column: x => x.ManufacturingRevisionId,
                        principalTable: "ManufacturingRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisualProposals_VisualProposals_ParentProposalId",
                        column: x => x.ParentProposalId,
                        principalTable: "VisualProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisualProposals_VisualSettingsRevisions_SettingsRevisionId",
                        column: x => x.SettingsRevisionId,
                        principalTable: "VisualSettingsRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisualProposals_ConversationId_Number",
                table: "VisualProposals",
                columns: new[] { "ConversationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisualProposals_ManufacturingRevisionId",
                table: "VisualProposals",
                column: "ManufacturingRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_VisualProposals_ParentProposalId",
                table: "VisualProposals",
                column: "ParentProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_VisualProposals_SettingsRevisionId",
                table: "VisualProposals",
                column: "SettingsRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_VisualProposals_Status_NextAttemptAt",
                table: "VisualProposals",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VisualSettingsRevisions_Version",
                table: "VisualSettingsRevisions",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisualConversationUsages");

            migrationBuilder.DropTable(
                name: "VisualProposals");

            migrationBuilder.DropTable(
                name: "VisualUserUsages");

            migrationBuilder.DropTable(
                name: "VisualSettingsRevisions");
        }
    }
}
