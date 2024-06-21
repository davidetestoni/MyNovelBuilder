using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyNovelBuilder.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class CompendiaGrammarFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompendiumNovel_Compendiums_CompendiumsId",
                table: "CompendiumNovel");

            migrationBuilder.DropForeignKey(
                name: "FK_CompendiumRecords_Compendiums_CompendiumId",
                table: "CompendiumRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Compendiums",
                table: "Compendiums");

            migrationBuilder.RenameTable(
                name: "Compendiums",
                newName: "Compendia");

            migrationBuilder.RenameColumn(
                name: "CompendiumsId",
                table: "CompendiumNovel",
                newName: "CompendiaId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Compendia",
                table: "Compendia",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CompendiumNovel_Compendia_CompendiaId",
                table: "CompendiumNovel",
                column: "CompendiaId",
                principalTable: "Compendia",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompendiumRecords_Compendia_CompendiumId",
                table: "CompendiumRecords",
                column: "CompendiumId",
                principalTable: "Compendia",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompendiumNovel_Compendia_CompendiaId",
                table: "CompendiumNovel");

            migrationBuilder.DropForeignKey(
                name: "FK_CompendiumRecords_Compendia_CompendiumId",
                table: "CompendiumRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Compendia",
                table: "Compendia");

            migrationBuilder.RenameTable(
                name: "Compendia",
                newName: "Compendiums");

            migrationBuilder.RenameColumn(
                name: "CompendiaId",
                table: "CompendiumNovel",
                newName: "CompendiumsId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Compendiums",
                table: "Compendiums",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CompendiumNovel_Compendiums_CompendiumsId",
                table: "CompendiumNovel",
                column: "CompendiumsId",
                principalTable: "Compendiums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompendiumRecords_Compendiums_CompendiumId",
                table: "CompendiumRecords",
                column: "CompendiumId",
                principalTable: "Compendiums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
