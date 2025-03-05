using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Changesv330 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComposition",
                table: "ZauberContentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFolder",
                table: "ZauberContentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsComposition",
                table: "ZauberContentTypes");

            migrationBuilder.DropColumn(
                name: "IsFolder",
                table: "ZauberContentTypes");
        }
    }
}
