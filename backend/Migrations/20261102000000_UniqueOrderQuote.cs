using AndEth3D.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AndEth3D.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102000000_UniqueOrderQuote")]
public partial class UniqueOrderQuote : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex(
        name: "IX_Orders_QuoteRequestId", table: "Orders", column: "QuoteRequestId", unique: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropIndex(
        name: "IX_Orders_QuoteRequestId", table: "Orders");
}
