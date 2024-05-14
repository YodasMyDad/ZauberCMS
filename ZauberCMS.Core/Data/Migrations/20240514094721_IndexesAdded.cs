using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndexesAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_Alias",
                table: "ZauberContentTypes",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_Name",
                table: "ZauberContentTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_Name",
                table: "ZauberContent",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_Url",
                table: "ZauberContent",
                column: "Url");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ZauberContentTypes_Alias",
                table: "ZauberContentTypes");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContentTypes_Name",
                table: "ZauberContentTypes");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_Name",
                table: "ZauberContent");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_Url",
                table: "ZauberContent");
        }
    }
}
