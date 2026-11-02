using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndEth3D.Backend.Migrations
{
    /// <inheritdoc />
    public partial class DesignChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatAllowances",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Conversations = table.Column<int>(type: "integer", nullable: false),
                    Requests = table.Column<int>(type: "integer", nullable: false),
                    LastRequestAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatAllowances", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ChatAllowances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ReservedUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    BusyUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BriefJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserText = table.Column<string>(type: "text", nullable: false),
                    AssistantText = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: true),
                    ReservedUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    ReportedCostUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: true),
                    ManufacturingRevisionId = table.Column<int>(type: "integer", nullable: true),
                    BriefJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatTurns_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatTurns_ManufacturingRevisions_ManufacturingRevisionId",
                        column: x => x.ManufacturingRevisionId,
                        principalTable: "ManufacturingRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId_UpdatedAt",
                table: "ChatConversations",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTurns_ConversationId_Sequence",
                table: "ChatTurns",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatTurns_ManufacturingRevisionId",
                table: "ChatTurns",
                column: "ManufacturingRevisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatAllowances");

            migrationBuilder.DropTable(
                name: "ChatTurns");

            migrationBuilder.DropTable(
                name: "ChatConversations");
        }
    }
}
