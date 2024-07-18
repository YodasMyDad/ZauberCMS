using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class UserEditAndPublished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastUpdatedById",
                table: "ZauberMedia",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastUpdatedById",
                table: "ZauberContentTypes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastUpdatedById",
                table: "ZauberContent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "ZauberContent",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_LastUpdatedById",
                table: "ZauberMedia",
                column: "LastUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_LastUpdatedById",
                table: "ZauberContentTypes",
                column: "LastUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_LastUpdatedById",
                table: "ZauberContent",
                column: "LastUpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberContent_ZauberUsers_LastUpdatedById",
                table: "ZauberContent",
                column: "LastUpdatedById",
                principalTable: "ZauberUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberContentTypes_ZauberUsers_LastUpdatedById",
                table: "ZauberContentTypes",
                column: "LastUpdatedById",
                principalTable: "ZauberUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberMedia_ZauberUsers_LastUpdatedById",
                table: "ZauberMedia",
                column: "LastUpdatedById",
                principalTable: "ZauberUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZauberContent_ZauberUsers_LastUpdatedById",
                table: "ZauberContent");

            migrationBuilder.DropForeignKey(
                name: "FK_ZauberContentTypes_ZauberUsers_LastUpdatedById",
                table: "ZauberContentTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ZauberMedia_ZauberUsers_LastUpdatedById",
                table: "ZauberMedia");

            migrationBuilder.DropIndex(
                name: "IX_ZauberMedia_LastUpdatedById",
                table: "ZauberMedia");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContentTypes_LastUpdatedById",
                table: "ZauberContentTypes");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_LastUpdatedById",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "LastUpdatedById",
                table: "ZauberMedia");

            migrationBuilder.DropColumn(
                name: "LastUpdatedById",
                table: "ZauberContentTypes");

            migrationBuilder.DropColumn(
                name: "LastUpdatedById",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "ZauberContent");
        }
    }
}
