using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class ContentVersionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentVersion_UniqueLatestDraft",
                table: "ZauberContentVersions");

            // Keep only one version of a page flagged as current published: the one published last, then the highest
            // version number, so the unique index can be created (Roadmap D14, ZA-050).
            migrationBuilder.Sql(@"
UPDATE ""ZauberContentVersions"" v SET ""IsCurrentPublished"" = FALSE
WHERE v.""IsCurrentPublished"" AND EXISTS (
    SELECT 1 FROM ""ZauberContentVersions"" n
    WHERE n.""ContentId"" = v.""ContentId"" AND n.""IsCurrentPublished""
      AND (n.""DatePublished"" > v.""DatePublished""
           OR (n.""DatePublished"" IS NOT NULL AND v.""DatePublished"" IS NULL)
           OR (n.""DatePublished"" IS NOT DISTINCT FROM v.""DatePublished""
               AND (n.""VersionNumber"" > v.""VersionNumber""
                    OR (n.""VersionNumber"" = v.""VersionNumber"" AND n.""Id"" > v.""Id"")))))");

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_UniqueCurrentPublished",
                table: "ZauberContentVersions",
                column: "ContentId",
                unique: true,
                filter: "\"IsCurrentPublished\"");

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_UniqueLatestDraft",
                table: "ZauberContentVersions",
                column: "ContentId",
                unique: true,
                filter: "\"IsLatestDraft\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentVersion_UniqueCurrentPublished",
                table: "ZauberContentVersions");

            migrationBuilder.DropIndex(
                name: "IX_ContentVersion_UniqueLatestDraft",
                table: "ZauberContentVersions");

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_UniqueLatestDraft",
                table: "ZauberContentVersions",
                column: "ContentId",
                unique: true,
                filter: "\"IsLatestDraft\"");  // as in the corrected Initial
        }
    }
}
