using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class AddIsNestedContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNestedContent",
                table: "ZauberContent",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_IsNestedContent",
                table: "ZauberContent",
                column: "IsNestedContent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ZauberContent_IsNestedContent",
                table: "ZauberContent");

            migrationBuilder.DropColumn(
                name: "IsNestedContent",
                table: "ZauberContent");
        }
    }
}
