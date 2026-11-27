using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    internal partial class ContentVersionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older versions could leave more than one version of a page flagged as current published. Keep the one
            // published last (a restored older version is published later than the newer ones), then the highest
            // version number, so the unique index can be created (Roadmap D14, ZA-050).
            migrationBuilder.Sql(@"
UPDATE ZauberContentVersions SET IsCurrentPublished = 0
WHERE IsCurrentPublished = 1 AND EXISTS (
    SELECT 1 FROM ZauberContentVersions n
    WHERE n.ContentId = ZauberContentVersions.ContentId AND n.IsCurrentPublished = 1
      AND (n.DatePublished > ZauberContentVersions.DatePublished
           OR (n.DatePublished IS NOT NULL AND ZauberContentVersions.DatePublished IS NULL)
           OR ((n.DatePublished = ZauberContentVersions.DatePublished
                OR (n.DatePublished IS NULL AND ZauberContentVersions.DatePublished IS NULL))
               AND (n.VersionNumber > ZauberContentVersions.VersionNumber
                    OR (n.VersionNumber = ZauberContentVersions.VersionNumber AND n.Id > ZauberContentVersions.Id)))))");

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersion_UniqueCurrentPublished",
                table: "ZauberContentVersions",
                column: "ContentId",
                unique: true,
                filter: "[IsCurrentPublished] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentVersion_UniqueCurrentPublished",
                table: "ZauberContentVersions");
        }
    }
}
