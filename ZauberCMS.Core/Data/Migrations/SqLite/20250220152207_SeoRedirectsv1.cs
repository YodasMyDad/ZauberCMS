using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class SeoRedirectsv1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZauberRedirects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DomainId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FromUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ToUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsPermanent = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "ZauberRedirects");
        }
    }
}
