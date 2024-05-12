using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContentAndContentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContentTypeId",
                table: "ZauberContent",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "ZauberContent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ZauberContent",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "ZauberContent",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_ContentTypeId",
                table: "ZauberContent",
                column: "ContentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberContent_ZauberContentTypes_ContentTypeId",
                table: "ZauberContent",
                column: "ContentTypeId",
                principalTable: "ZauberContentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZauberContent_ZauberContentTypes_ContentTypeId",
                table: "ZauberContent");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_ContentTypeId",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "ContentTypeId",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "ZauberContent");
        }
    }
}
