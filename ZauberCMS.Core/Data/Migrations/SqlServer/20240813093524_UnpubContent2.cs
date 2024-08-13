using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class UnpubContent2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ZauberUnpublishedContent_ContentId",
                table: "ZauberUnpublishedContent");

            migrationBuilder.DropColumn(
                name: "ContentId",
                table: "ZauberUnpublishedContent");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "ZauberUnpublishedContent",
                newName: "JsonContent");

            migrationBuilder.AddColumn<Guid>(
                name: "UnpublishedContentId",
                table: "ZauberContent",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_UnpublishedContentId",
                table: "ZauberContent",
                column: "UnpublishedContentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberContent_ZauberUnpublishedContent_UnpublishedContentId",
                table: "ZauberContent",
                column: "UnpublishedContentId",
                principalTable: "ZauberUnpublishedContent",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZauberContent_ZauberUnpublishedContent_UnpublishedContentId",
                table: "ZauberContent");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_UnpublishedContentId",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "UnpublishedContentId",
                table: "ZauberContent");

            migrationBuilder.RenameColumn(
                name: "JsonContent",
                table: "ZauberUnpublishedContent",
                newName: "Content");

            migrationBuilder.AddColumn<Guid>(
                name: "ContentId",
                table: "ZauberUnpublishedContent",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUnpublishedContent_ContentId",
                table: "ZauberUnpublishedContent",
                column: "ContentId");
        }
    }
}
