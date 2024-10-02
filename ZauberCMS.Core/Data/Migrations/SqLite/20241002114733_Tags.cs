﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class Tags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZauberTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberTagItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberTagItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberTagItems_ZauberTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ZauberTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZauberTagItems_TagId",
                table: "ZauberTagItems",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberTag_TagName",
                table: "ZauberTags",
                column: "TagName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberTagItems");

            migrationBuilder.DropTable(
                name: "ZauberTags");
        }
    }
}
