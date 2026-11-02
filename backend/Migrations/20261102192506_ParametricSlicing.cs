using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndEth3D.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ParametricSlicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParametricJobId",
                table: "QuoteRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParametricAllowances",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametricAllowances", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ParametricAllowances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParametricJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturingRevisionId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BriefJson = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    ParametersJson = table.Column<string>(type: "text", nullable: false),
                    TemplateVersion = table.Column<string>(type: "text", nullable: false),
                    ModelStl = table.Column<byte[]>(type: "bytea", nullable: false),
                    ModelHash = table.Column<string>(type: "text", nullable: false),
                    ProfileJson = table.Column<string>(type: "text", nullable: true),
                    ProfileIni = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Failure = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Grams = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Seconds = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    EstimateJson = table.Column<string>(type: "text", nullable: true),
                    ReproductionHash = table.Column<string>(type: "text", nullable: true),
                    ActualGrams = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    ActualSeconds = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    MeasurementNotes = table.Column<string>(type: "text", nullable: true),
                    MeasuredBy = table.Column<int>(type: "integer", nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametricJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParametricJobs_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParametricJobs_ManufacturingRevisions_ManufacturingRevision~",
                        column: x => x.ManufacturingRevisionId,
                        principalTable: "ManufacturingRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRequests_ParametricJobId",
                table: "QuoteRequests",
                column: "ParametricJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ParametricJobs_ConversationId",
                table: "ParametricJobs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ParametricJobs_ManufacturingRevisionId",
                table: "ParametricJobs",
                column: "ManufacturingRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ParametricJobs_Status_CreatedAt",
                table: "ParametricJobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteRequests_ParametricJobs_ParametricJobId",
                table: "QuoteRequests",
                column: "ParametricJobId",
                principalTable: "ParametricJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteRequests_ParametricJobs_ParametricJobId",
                table: "QuoteRequests");

            migrationBuilder.DropTable(
                name: "ParametricAllowances");

            migrationBuilder.DropTable(
                name: "ParametricJobs");

            migrationBuilder.DropIndex(
                name: "IX_QuoteRequests_ParametricJobId",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "ParametricJobId",
                table: "QuoteRequests");
        }
    }
}
