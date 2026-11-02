using AndEth3D.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace AndEth3D.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102000002_ManufacturingConfiguration")]
public partial class ManufacturingConfiguration : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "ManufacturingRevisions", columns: table => new {
            Id = table.Column<int>(type: "integer", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Version = table.Column<int>(type: "integer", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
            ConfigurationJson = table.Column<string>(type: "text", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ManufacturingRevisions", x => x.Id));
        m.CreateIndex(name: "IX_ManufacturingRevisions_Version", table: "ManufacturingRevisions", column: "Version", unique: true);
        m.AddColumn<int>(name: "ManufacturingRevisionId", table: "QuoteRequests", type: "integer", nullable: true);
        m.CreateIndex(name: "IX_QuoteRequests_ManufacturingRevisionId", table: "QuoteRequests", column: "ManufacturingRevisionId");
        m.AddForeignKey(name: "FK_QuoteRequests_ManufacturingRevisions_ManufacturingRevisionId", table: "QuoteRequests",
            column: "ManufacturingRevisionId", principalTable: "ManufacturingRevisions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropForeignKey(name: "FK_QuoteRequests_ManufacturingRevisions_ManufacturingRevisionId", table: "QuoteRequests");
        m.DropIndex(name: "IX_QuoteRequests_ManufacturingRevisionId", table: "QuoteRequests");
        m.DropColumn(name: "ManufacturingRevisionId", table: "QuoteRequests");
        m.DropTable(name: "ManufacturingRevisions");
    }
}
