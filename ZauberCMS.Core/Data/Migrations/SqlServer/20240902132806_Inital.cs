using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Inital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZauberGlobalData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberGlobalData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberLanguageDictionaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberLanguageDictionaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberLanguages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageIsoCode = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    LanguageCultureName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberLanguages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtendedData = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tabs = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberUnpublishedContent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JsonContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUnpublishedContent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtendedData = table.Column<string>(type: "nvarchar(3500)", maxLength: 3500, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZauberDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: true),
                    LanguageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberDomains_ZauberLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "ZauberLanguages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ZauberLanguageTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageDictionaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberLanguageTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberLanguageTexts_ZauberLanguageDictionaries_LanguageDictionaryId",
                        column: x => x.LanguageDictionaryId,
                        principalTable: "ZauberLanguageDictionaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZauberLanguageTexts_ZauberLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "ZauberLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZauberRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true)
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
                name: "ZauberContentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Alias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsElementType = table.Column<bool>(type: "bit", nullable: false),
                    AllowAtRoot = table.Column<bool>(type: "bit", nullable: false),
                    EnableListView = table.Column<bool>(type: "bit", nullable: false),
                    IncludeChildren = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContentProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvailableContentViews = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AllowedChildContentTypes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Tabs = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberContentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberContentTypes_ZauberUsers_LastUpdatedById",
                        column: x => x.LastUpdatedById,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ZauberMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AltTag = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MediaType = table.Column<int>(type: "int", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<long>(type: "bigint", nullable: false),
                    Height = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtendedData = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberMedia_ZauberMedia_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ZauberMedia",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberMedia_ZauberUsers_LastUpdatedById",
                        column: x => x.LastUpdatedById,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ZauberUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true)
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
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "ZauberUserPropertyValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentTypePropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberUserPropertyValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberUserPropertyValues_ZauberUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ZauberUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "ZauberContent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentTypeAlias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastUpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnpublishedContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsRootContent = table.Column<bool>(type: "bit", nullable: false),
                    Published = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    HideFromNavigation = table.Column<bool>(type: "bit", nullable: false),
                    InternalRedirectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ViewComponent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LanguageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
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
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberContent_ZauberLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "ZauberLanguages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberContent_ZauberUnpublishedContent_UnpublishedContentId",
                        column: x => x.UnpublishedContentId,
                        principalTable: "ZauberUnpublishedContent",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberContent_ZauberUsers_LastUpdatedById",
                        column: x => x.LastUpdatedById,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ZauberAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberAudits_ZauberContent_ContentId",
                        column: x => x.ContentId,
                        principalTable: "ZauberContent",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberAudits_ZauberMedia_MediaId",
                        column: x => x.MediaId,
                        principalTable: "ZauberMedia",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZauberAudits_ZauberUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ZauberUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ZauberContentPropertyValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentTypePropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZauberContentPropertyValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZauberContentPropertyValues_ZauberContent_ContentId",
                        column: x => x.ContentId,
                        principalTable: "ZauberContent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZauberAudits_ContentId",
                table: "ZauberAudits",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberAudits_MediaId",
                table: "ZauberAudits",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberAudits_UserId",
                table: "ZauberAudits",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_ContentTypeId",
                table: "ZauberContent",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_LanguageId",
                table: "ZauberContent",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_LastUpdatedById",
                table: "ZauberContent",
                column: "LastUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_Name",
                table: "ZauberContent",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_ParentId",
                table: "ZauberContent",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_UnpublishedContentId",
                table: "ZauberContent",
                column: "UnpublishedContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContent_Url",
                table: "ZauberContent",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentPropertyValue_Alias",
                table: "ZauberContentPropertyValues",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentPropertyValues_ContentId",
                table: "ZauberContentPropertyValues",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_Alias",
                table: "ZauberContentTypes",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_LastUpdatedById",
                table: "ZauberContentTypes",
                column: "LastUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberContentTypes_Name",
                table: "ZauberContentTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberDomains_LanguageId",
                table: "ZauberDomains",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalDataAlias",
                table: "ZauberGlobalData",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageDictionary_Key",
                table: "ZauberLanguageDictionaries",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageText_Value",
                table: "ZauberLanguageTexts",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberLanguageTexts_LanguageDictionaryId",
                table: "ZauberLanguageTexts",
                column: "LanguageDictionaryId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberLanguageTexts_LanguageId",
                table: "ZauberLanguageTexts",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberMedia_LastUpdatedById",
                table: "ZauberMedia",
                column: "LastUpdatedById");

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
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUserClaims_UserId",
                table: "ZauberUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUserLogins_UserId",
                table: "ZauberUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUserPropertyValue_Alias",
                table: "ZauberUserPropertyValues",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ZauberUserPropertyValues_UserId",
                table: "ZauberUserPropertyValues",
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
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZauberAudits");

            migrationBuilder.DropTable(
                name: "ZauberContentPropertyValues");

            migrationBuilder.DropTable(
                name: "ZauberDomains");

            migrationBuilder.DropTable(
                name: "ZauberGlobalData");

            migrationBuilder.DropTable(
                name: "ZauberLanguageTexts");

            migrationBuilder.DropTable(
                name: "ZauberRoleClaims");

            migrationBuilder.DropTable(
                name: "ZauberUserClaims");

            migrationBuilder.DropTable(
                name: "ZauberUserLogins");

            migrationBuilder.DropTable(
                name: "ZauberUserPropertyValues");

            migrationBuilder.DropTable(
                name: "ZauberUserRoles");

            migrationBuilder.DropTable(
                name: "ZauberUserTokens");

            migrationBuilder.DropTable(
                name: "ZauberMedia");

            migrationBuilder.DropTable(
                name: "ZauberContent");

            migrationBuilder.DropTable(
                name: "ZauberLanguageDictionaries");

            migrationBuilder.DropTable(
                name: "ZauberRoles");

            migrationBuilder.DropTable(
                name: "ZauberContentTypes");

            migrationBuilder.DropTable(
                name: "ZauberLanguages");

            migrationBuilder.DropTable(
                name: "ZauberUnpublishedContent");

            migrationBuilder.DropTable(
                name: "ZauberUsers");
        }
    }
}
