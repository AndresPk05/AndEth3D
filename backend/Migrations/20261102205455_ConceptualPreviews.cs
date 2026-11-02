using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndEth3D.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ConceptualPreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ManufacturingRevisionId",
                table: "VisualProposals",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "VisualProposals" WHERE "ManufacturingRevisionId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back while conceptual previews without a manufacturing revision exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<int>(
                name: "ManufacturingRevisionId",
                table: "VisualProposals",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
