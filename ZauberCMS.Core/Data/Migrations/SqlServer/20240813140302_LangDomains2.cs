using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class LangDomains2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "ZauberContent");

            migrationBuilder.AddColumn<Guid>(
                name: "LanguageId",
                table: "ZauberContent",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_LanguageId",
                table: "ZauberContent",
                column: "LanguageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberContent_ZauberLanguages_LanguageId",
                table: "ZauberContent",
                column: "LanguageId",
                principalTable: "ZauberLanguages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZauberContent_ZauberLanguages_LanguageId",
                table: "ZauberContent");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_LanguageId",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "LanguageId",
                table: "ZauberContent");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ZauberContent",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);
        }
    }
}
