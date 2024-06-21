using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoleUpdateOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RoleProperties",
                table: "ZauberRoles",
                newName: "Tabs");

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "ZauberRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Properties",
                table: "ZauberRoles");

            migrationBuilder.RenameColumn(
                name: "Tabs",
                table: "ZauberRoles",
                newName: "RoleProperties");
        }
    }
}
