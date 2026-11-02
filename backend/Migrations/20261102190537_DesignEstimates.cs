using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AndEth3D.Backend.Migrations
{
    /// <inheritdoc />
    public partial class DesignEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConfirmedPriceCop",
                table: "QuoteRequests",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesignEstimateToken",
                table: "QuoteRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DesignRequestId",
                table: "QuoteRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesignSnapshotJson",
                table: "QuoteRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposalId",
                table: "QuoteRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewVersion",
                table: "QuoteRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CalibrationRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ManufacturingRevisionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SamplesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationRevisions_ManufacturingRevisions_ManufacturingRe~",
                        column: x => x.ManufacturingRevisionId,
                        principalTable: "ManufacturingRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRequests_DesignRequestId",
                table: "QuoteRequests",
                column: "DesignRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRequests_ProposalId",
                table: "QuoteRequests",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRevisions_ManufacturingRevisionId",
                table: "CalibrationRevisions",
                column: "ManufacturingRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRevisions_Version",
                table: "CalibrationRevisions",
                column: "Version",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteRequests_VisualProposals_ProposalId",
                table: "QuoteRequests",
                column: "ProposalId",
                principalTable: "VisualProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteRequests_VisualProposals_ProposalId",
                table: "QuoteRequests");

            migrationBuilder.DropTable(
                name: "CalibrationRevisions");

            migrationBuilder.DropIndex(
                name: "IX_QuoteRequests_DesignRequestId",
                table: "QuoteRequests");

            migrationBuilder.DropIndex(
                name: "IX_QuoteRequests_ProposalId",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmedPriceCop",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "DesignEstimateToken",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "DesignRequestId",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "DesignSnapshotJson",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "ProposalId",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "ReviewVersion",
                table: "QuoteRequests");
        }
    }
}
