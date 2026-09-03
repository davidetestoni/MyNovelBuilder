using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyNovelBuilder.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class CompendiumRecordAlwaysIncluded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlwaysIncluded",
                table: "CompendiumRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlwaysIncluded",
                table: "CompendiumRecords");
        }
    }
}
