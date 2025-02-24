using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Changesv320 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "ZauberMedia",
                type: "nvarchar(3000)",
                maxLength: 3000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ZauberMedia",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ZauberContentTypes",
                type: "nvarchar(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaId",
                table: "ZauberContentTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "ZauberContentTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ZauberContentRole",
                columns: table => new
                {
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberContentRole", x => new { x.ContentId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_ZauberContentRole_ZauberContent_ContentId",
                        column: x => x.ContentId,
                        principalTable: "ZauberContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZauberContentRole_ZauberRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "ZauberRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZauberMediaRole",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberMediaRole", x => new { x.MediaId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_ZauberMediaRole_ZauberMedia_MediaId",
                        column: x => x.MediaId,
                        principalTable: "ZauberMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZauberMediaRole_ZauberRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "ZauberRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZauberRedirects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ToUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsPermanent = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberRedirects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberRedirects_ZauberDomains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "ZauberDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_Path",
                table: "ZauberMedia",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_Path",
                table: "ZauberContent",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentRole_RoleId",
                table: "ZauberContentRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMediaRole_RoleId",
                table: "ZauberMediaRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberRedirects_DomainId",
                table: "ZauberRedirects",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberRedirects_FromUrl",
                table: "ZauberRedirects",
                column: "FromUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberContentRole");

            migrationBuilder.DropTable(
                name: "ZauberMediaRole");

            migrationBuilder.DropTable(
                name: "ZauberRedirects");

            migrationBuilder.DropIndex(
                name: "IX_ZauberMedia_Path",
                table: "ZauberMedia");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_Path",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "ZauberMedia");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ZauberMedia");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ZauberContentTypes");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "ZauberContentTypes");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ZauberContentTypes");
        }
    }
}
