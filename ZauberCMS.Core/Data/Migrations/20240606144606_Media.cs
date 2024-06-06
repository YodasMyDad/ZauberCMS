using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Media : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZauberMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Width = table.Column<long>(type: "INTEGER", nullable: false),
                    Height = table.Column<long>(type: "INTEGER", nullable: false),
                    IsTemp = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExtendedData = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberMedia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_Name",
                table: "ZauberMedia",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_Url",
                table: "ZauberMedia",
                column: "Url");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberMedia");
        }
    }
}
