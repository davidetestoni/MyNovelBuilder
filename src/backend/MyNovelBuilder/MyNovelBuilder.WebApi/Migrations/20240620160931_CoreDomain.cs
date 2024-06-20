using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyNovelBuilder.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class CoreDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Compendiums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compendiums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Messages = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompendiumRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Aliases = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CompendiumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompendiumRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompendiumRecords_Compendiums_CompendiumId",
                        column: x => x.CompendiumId,
                        principalTable: "Compendiums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Novels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Brief = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Tense = table.Column<int>(type: "INTEGER", nullable: false),
                    Pov = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<int>(type: "INTEGER", nullable: false),
                    MainCharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Novels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Novels_CompendiumRecords_MainCharacterId",
                        column: x => x.MainCharacterId,
                        principalTable: "CompendiumRecords",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompendiumNovel",
                columns: table => new
                {
                    CompendiumsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NovelsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompendiumNovel", x => new { x.CompendiumsId, x.NovelsId });
                    table.ForeignKey(
                        name: "FK_CompendiumNovel_Compendiums_CompendiumsId",
                        column: x => x.CompendiumsId,
                        principalTable: "Compendiums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompendiumNovel_Novels_NovelsId",
                        column: x => x.NovelsId,
                        principalTable: "Novels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompendiumNovel_NovelsId",
                table: "CompendiumNovel",
                column: "NovelsId");

            migrationBuilder.CreateIndex(
                name: "IX_CompendiumRecords_CompendiumId",
                table: "CompendiumRecords",
                column: "CompendiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Novels_MainCharacterId",
                table: "Novels",
                column: "MainCharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompendiumNovel");

            migrationBuilder.DropTable(
                name: "Prompts");

            migrationBuilder.DropTable(
                name: "Novels");

            migrationBuilder.DropTable(
                name: "CompendiumRecords");

            migrationBuilder.DropTable(
                name: "Compendiums");
        }
    }
}
