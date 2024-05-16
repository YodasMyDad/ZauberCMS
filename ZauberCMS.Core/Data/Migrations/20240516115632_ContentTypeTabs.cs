using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContentTypeTabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tabs",
                table: "ZauberContentTypes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tabs",
                table: "ZauberContentTypes");
        }
    }
}
