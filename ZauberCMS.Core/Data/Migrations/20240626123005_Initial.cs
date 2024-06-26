using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZauberAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberContentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsElementType = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowAtRoot = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContentProperties = table.Column<string>(type: "TEXT", nullable: false),
                    AvailableContentViews = table.Column<string>(type: "TEXT", nullable: false),
                    Tabs = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberContentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberGlobalData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberGlobalData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Width = table.Column<long>(type: "INTEGER", nullable: false),
                    Height = table.Column<long>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExtendedData = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberMedia_ZauberMedia_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ZauberMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ZauberRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExtendedData = table.Column<string>(type: "TEXT", nullable: false),
                    Properties = table.Column<string>(type: "TEXT", nullable: false),
                    Tabs = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PropertyData = table.Column<string>(type: "TEXT", nullable: false),
                    ExtendedData = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberContent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ContentTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeAlias = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRootContent = table.Column<bool>(type: "INTEGER", nullable: false),
                    HideFromNavigation = table.Column<bool>(type: "INTEGER", nullable: false),
                    InternalRedirectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ViewComponent = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ContentPropertyData = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberContent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberContent_ZauberContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ZauberContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZauberContent_ZauberContent_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ZauberContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ZauberRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberRoleClaims_ZauberRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "ZauberRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZauberUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberUserClaims_ZauberUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZauberUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_ZauberUserLogins_ZauberUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZauberUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_ZauberUserRoles_ZauberRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "ZauberRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZauberUserRoles_ZauberUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZauberUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_ZauberUserTokens_ZauberUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZauberAudits_Username",
                table: "ZauberAudits",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_ContentTypeId",
                table: "ZauberContent",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_Name",
                table: "ZauberContent",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_ParentId",
                table: "ZauberContent",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_Url",
                table: "ZauberContent",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_Alias",
                table: "ZauberContentTypes",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_Name",
                table: "ZauberContentTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalDataAlias",
                table: "ZauberGlobalData",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_Name",
                table: "ZauberMedia",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_ParentId",
                table: "ZauberMedia",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_Url",
                table: "ZauberMedia",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberRoleClaims_RoleId",
                table: "ZauberRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "ZauberRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUserClaims_UserId",
                table: "ZauberUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUserLogins_UserId",
                table: "ZauberUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUserRoles_RoleId",
                table: "ZauberUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "ZauberUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "ZauberUsers",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberAudits");

            migrationBuilder.DropTable(
                name: "ZauberContent");

            migrationBuilder.DropTable(
                name: "ZauberGlobalData");

            migrationBuilder.DropTable(
                name: "ZauberMedia");

            migrationBuilder.DropTable(
                name: "ZauberRoleClaims");

            migrationBuilder.DropTable(
                name: "ZauberUserClaims");

            migrationBuilder.DropTable(
                name: "ZauberUserLogins");

            migrationBuilder.DropTable(
                name: "ZauberUserRoles");

            migrationBuilder.DropTable(
                name: "ZauberUserTokens");

            migrationBuilder.DropTable(
                name: "ZauberContentTypes");

            migrationBuilder.DropTable(
                name: "ZauberRoles");

            migrationBuilder.DropTable(
                name: "ZauberUsers");
        }
    }
}
