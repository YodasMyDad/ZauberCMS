using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class TagsRemoveFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZauberTagItems_ZauberContent_ItemId",
                table: "ZauberTagItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_ZauberTagItems_ZauberContent_ItemId",
                table: "ZauberTagItems",
                column: "ItemId",
                principalTable: "ZauberContent",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
