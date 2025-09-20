using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class AddContentVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZauberContentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Snapshot = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    PropertySnapshots = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatePublished = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Comments = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsCurrentPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLatestDraft = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentVersionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IsAutoSave = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ContentSize = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberContentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberContentVersions_ZauberContentVersions_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalTable: "ZauberContentVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberContentVersions_ZauberUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_ContentId_Status",
                table: "ZauberContentVersions",
                columns: new[] { "ContentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_ContentId_Version",
                table: "ZauberContentVersions",
                columns: new[] { "ContentId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_CurrentPublished",
                table: "ZauberContentVersions",
                columns: new[] { "ContentId", "IsCurrentPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_DateCreated",
                table: "ZauberContentVersions",
                column: "DateCreated");

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_LatestDraft",
                table: "ZauberContentVersions",
                columns: new[] { "ContentId", "IsLatestDraft" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_UniqueLatestDraft",
                table: "ZauberContentVersions",
                column: "ContentId",
                unique: true,
                filter: "[IsLatestDraft] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentVersions_CreatedById",
                table: "ZauberContentVersions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentVersions_ParentVersionId",
                table: "ZauberContentVersions",
                column: "ParentVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberContentVersions");
        }
    }
}
