using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParentAndChildrenContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_ParentId",
                table: "ZauberContent",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZauberContent_ZauberContent_ParentId",
                table: "ZauberContent",
                column: "ParentId",
                principalTable: "ZauberContent",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZauberContent_ZauberContent_ParentId",
                table: "ZauberContent");

            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_ParentId",
                table: "ZauberContent");
        }
    }
}
