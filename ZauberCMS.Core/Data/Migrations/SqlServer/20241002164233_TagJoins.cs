﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class TagJoins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ZauberTagItems_ItemId",
                table: "ZauberTagItems",
                column: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberTagItems_ZauberContent_ItemId",
                table: "ZauberTagItems",
                column: "ItemId",
                principalTable: "ZauberContent",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZauberTagItems_ZauberContent_ItemId",
                table: "ZauberTagItems");

            migrationBuilder.DropIndex(
                name: "IX_ZauberTagItems_ItemId",
                table: "ZauberTagItems");
        }
    }
}