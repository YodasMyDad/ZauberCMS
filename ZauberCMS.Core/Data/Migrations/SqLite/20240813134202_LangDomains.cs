using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class LangDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ZauberContent",
                type: "TEXT",
                maxLength: 7,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ZauberLanguages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LanguageIsoCode = table.Column<string>(type: "TEXT", maxLength: 14, nullable: true),
                    LanguageCultureName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberLanguages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 350, nullable: true),
                    LanguageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberDomains_ZauberLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "ZauberLanguages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZauberDomains_LanguageId",
                table: "ZauberDomains",
                column: "LanguageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberDomains");

            migrationBuilder.DropTable(
                name: "ZauberLanguages");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "ZauberContent");
        }
    }
}
