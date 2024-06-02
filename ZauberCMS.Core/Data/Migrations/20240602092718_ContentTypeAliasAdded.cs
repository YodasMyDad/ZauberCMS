using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContentTypeAliasAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentTypeAlias",
                table: "ZauberContent",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentTypeAlias",
                table: "ZauberContent");
        }
    }
}
