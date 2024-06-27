using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyValuesToTable3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PropertyData",
                table: "ZauberUsers");

            migrationBuilder.DropColumn(
                name: "ContentPropertyData",
                table: "ZauberContent");

            migrationBuilder.CreateTable(
                name: "ZauberPropertyValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContentTypePropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberPropertyValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberPropertyValues_ZauberContent_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ZauberContent",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberPropertyValues_ZauberUsers_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZauberPropertyValues_Alias",
                table: "ZauberPropertyValues",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberPropertyValues_ParentId",
                table: "ZauberPropertyValues",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberPropertyValues");

            migrationBuilder.AddColumn<string>(
                name: "PropertyData",
                table: "ZauberUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContentPropertyData",
                table: "ZauberContent",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
