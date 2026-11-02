using AndEth3D.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace AndEth3D.Backend.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261102000001_SessionVersion")]
public partial class SessionVersion : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<int>(name: "SessionVersion", table: "Users", type: "integer", nullable: false, defaultValue: 0);
    protected override void Down(MigrationBuilder m) => m.DropColumn(name: "SessionVersion", table: "Users");
}
