using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class LanguageStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZauberLanguageDictionaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberLanguageDictionaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberLanguageTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LanguageDictionaryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LanguageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberLanguageTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberLanguageTexts_ZauberLanguageDictionaries_LanguageDictionaryId",
                        column: x => x.LanguageDictionaryId,
                        principalTable: "ZauberLanguageDictionaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZauberLanguageTexts_ZauberLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "ZauberLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LanguageDictionary_Key",
                table: "ZauberLanguageDictionaries",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageText_Value",
                table: "ZauberLanguageTexts",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberLanguageTexts_LanguageDictionaryId",
                table: "ZauberLanguageTexts",
                column: "LanguageDictionaryId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberLanguageTexts_LanguageId",
                table: "ZauberLanguageTexts",
                column: "LanguageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberLanguageTexts");

            migrationBuilder.DropTable(
                name: "ZauberLanguageDictionaries");
        }
    }
}
