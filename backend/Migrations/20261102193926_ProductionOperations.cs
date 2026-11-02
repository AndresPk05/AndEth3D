using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndEth3D.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ProductionOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ManufacturingRevisionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<string>(type: "text", nullable: false),
                    MaterialId = table.Column<string>(type: "text", nullable: false),
                    PrinterId = table.Column<string>(type: "text", nullable: false),
                    Complexity = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ColorCount = table.Column<int>(type: "integer", nullable: true),
                    WidthMm = table.Column<decimal>(type: "numeric", nullable: true),
                    DepthMm = table.Column<decimal>(type: "numeric", nullable: true),
                    HeightMm = table.Column<decimal>(type: "numeric", nullable: true),
                    Grams = table.Column<decimal>(type: "numeric", nullable: false),
                    MachineHours = table.Column<decimal>(type: "numeric", nullable: false),
                    DesignHours = table.Column<decimal>(type: "numeric", nullable: false),
                    FinishingHours = table.Column<decimal>(type: "numeric", nullable: false),
                    MaterialCostCop = table.Column<decimal>(type: "numeric", nullable: false),
                    MachineCostCop = table.Column<decimal>(type: "numeric", nullable: false),
                    LaborCostCop = table.Column<decimal>(type: "numeric", nullable: false),
                    OtherCostCop = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimatedTotalCop = table.Column<decimal>(type: "numeric", nullable: true),
                    ConfirmedTotalCop = table.Column<decimal>(type: "numeric", nullable: true),
                    EstimatedGrams = table.Column<decimal>(type: "numeric", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRecords_ManufacturingRevisions_ManufacturingRevis~",
                        column: x => x.ManufacturingRevisionId,
                        principalTable: "ManufacturingRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRecords_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecords_CreatedAt",
                table: "ProductionRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecords_ManufacturingRevisionId",
                table: "ProductionRecords",
                column: "ManufacturingRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecords_OrderId",
                table: "ProductionRecords",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionRecords");
        }
    }
}
