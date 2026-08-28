using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyNovelBuilder.WebApi.Data;

#nullable disable

namespace MyNovelBuilder.WebApi.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260828160000_AddInitializationMarkers")]
public partial class AddInitializationMarkers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InitializationMarkers",
            columns: table => new
            {
                Key = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                CompletedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InitializationMarkers", x => x.Key);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InitializationMarkers");
    }
}
