﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZauberCMS.Core.Data;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    [DbContext(typeof(ZauberDbContext))]
    partial class ZauberDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("ZauberCMS.Core.Audit.Models.Audit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ContentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<Guid?>("MediaId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ContentId");

                    b.HasIndex("MediaId");

                    b.HasIndex("UserId");

                    b.ToTable("ZauberAudits", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.Content", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ContentTypeAlias")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<Guid>("ContentTypeId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<bool>("Deleted")
                        .HasColumnType("bit");

                    b.Property<bool>("HideFromNavigation")
                        .HasColumnType("bit");

                    b.Property<Guid?>("InternalRedirectId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsRootContent")
                        .HasColumnType("bit");

                    b.Property<Guid?>("LanguageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("LastUpdatedById")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Name")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<Guid?>("ParentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<bool>("Published")
                        .HasColumnType("bit");

                    b.Property<int>("SortOrder")
                        .HasColumnType("int");

                    b.Property<Guid?>("UnpublishedContentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Url")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<string>("ViewComponent")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.HasKey("Id");

                    b.HasIndex("ContentTypeId");

                    b.HasIndex("LanguageId");

                    b.HasIndex("LastUpdatedById");

                    b.HasIndex("Name")
                        .HasDatabaseName("IX_ZauberContent_Name");

                    b.HasIndex("ParentId");

                    b.HasIndex("UnpublishedContentId");

                    b.HasIndex("Url")
                        .HasDatabaseName("IX_ZauberContent_Url");

                    b.ToTable("ZauberContent", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.ContentPropertyValue", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Alias")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<Guid>("ContentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ContentTypePropertyId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("Alias")
                        .HasDatabaseName("IX_ZauberContentPropertyValue_Alias");

                    b.HasIndex("ContentId");

                    b.ToTable("ZauberContentPropertyValues", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.ContentType", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Alias")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<bool>("AllowAtRoot")
                        .HasColumnType("bit");

                    b.Property<string>("AllowedChildContentTypes")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<string>("AvailableContentViews")
                        .IsRequired()
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<string>("ContentProperties")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .HasMaxLength(1500)
                        .HasColumnType("nvarchar(1500)");

                    b.Property<bool>("EnableListView")
                        .HasColumnType("bit");

                    b.Property<string>("Icon")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<bool>("IncludeChildren")
                        .HasColumnType("bit");

                    b.Property<bool>("IsElementType")
                        .HasColumnType("bit");

                    b.Property<Guid?>("LastUpdatedById")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("MediaId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Name")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<Guid?>("ParentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Tabs")
                        .IsRequired()
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.HasKey("Id");

                    b.HasIndex("Alias")
                        .HasDatabaseName("IX_ZauberContentTypes_Alias");

                    b.HasIndex("LastUpdatedById");

                    b.HasIndex("Name")
                        .HasDatabaseName("IX_ZauberContentTypes_Name");

                    b.ToTable("ZauberContentTypes", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.Domain", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ContentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("LanguageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Url")
                        .HasMaxLength(350)
                        .HasColumnType("nvarchar(350)");

                    b.HasKey("Id");

                    b.HasIndex("LanguageId");

                    b.ToTable("ZauberDomains", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.UnpublishedContent", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("JsonContent")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("ZauberUnpublishedContent", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Data.Models.GlobalData", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Alias")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<string>("Data")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.HasIndex("Alias")
                        .HasDatabaseName("IX_GlobalDataAlias");

                    b.ToTable("ZauberGlobalData", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Languages.Models.Language", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("LanguageCultureName")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("LanguageIsoCode")
                        .HasMaxLength(14)
                        .HasColumnType("nvarchar(14)");

                    b.HasKey("Id");

                    b.ToTable("ZauberLanguages", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Languages.Models.LanguageDictionary", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.HasKey("Id");

                    b.HasIndex("Key")
                        .HasDatabaseName("IX_LanguageDictionary_Key");

                    b.ToTable("ZauberLanguageDictionaries", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Languages.Models.LanguageText", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("LanguageDictionaryId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("LanguageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.HasKey("Id");

                    b.HasIndex("LanguageDictionaryId");

                    b.HasIndex("LanguageId");

                    b.HasIndex("Value")
                        .HasDatabaseName("IX_LanguageText_Value");

                    b.ToTable("ZauberLanguageTexts", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Media.Models.Media", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("AltTag")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<bool>("Deleted")
                        .HasColumnType("bit");

                    b.Property<string>("ExtendedData")
                        .IsRequired()
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<long>("FileSize")
                        .HasColumnType("bigint");

                    b.Property<long>("Height")
                        .HasColumnType("bigint");

                    b.Property<Guid?>("LastUpdatedById")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("MediaType")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<Guid?>("ParentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Url")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<long>("Width")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("LastUpdatedById");

                    b.HasIndex("Name")
                        .HasDatabaseName("IX_ZauberMedia_Name");

                    b.HasIndex("ParentId");

                    b.HasIndex("Url")
                        .HasDatabaseName("IX_ZauberMedia_Url");

                    b.ToTable("ZauberMedia", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.Role", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("ExtendedData")
                        .IsRequired()
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<string>("Icon")
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("Properties")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Tabs")
                        .IsRequired()
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex")
                        .HasFilter("[NormalizedName] IS NOT NULL");

                    b.ToTable("ZauberRoles", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.RoleClaim", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<string>("ClaimValue")
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<Guid>("RoleId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("ZauberRoleClaims", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("ExtendedData")
                        .IsRequired()
                        .HasMaxLength(3500)
                        .HasColumnType("nvarchar(3500)");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("PasswordHash")
                        .HasMaxLength(300)
                        .HasColumnType("nvarchar(300)");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("SecurityStamp")
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("bit");

                    b.Property<string>("UserName")
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex")
                        .HasFilter("[NormalizedUserName] IS NOT NULL");

                    b.ToTable("ZauberUsers", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserClaim", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<string>("ClaimValue")
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("ZauberUserClaims", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserLogin", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderKey")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderDisplayName")
                        .HasMaxLength(3000)
                        .HasColumnType("nvarchar(3000)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("ZauberUserLogins", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserPropertyValue", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Alias")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<Guid>("ContentTypePropertyId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("Alias")
                        .HasDatabaseName("IX_ZauberUserPropertyValue_Alias");

                    b.HasIndex("UserId");

                    b.ToTable("ZauberUserPropertyValues", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserRole", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("RoleId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("ZauberUserRoles", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserToken", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Value")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("ZauberUserTokens", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Tags.Models.Tag", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<string>("Slug")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<int>("SortOrder")
                        .HasColumnType("int");

                    b.Property<string>("TagName")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.HasKey("Id");

                    b.HasIndex("Slug")
                        .HasDatabaseName("IX_ZauberTag_Slug");

                    b.HasIndex("TagName")
                        .HasDatabaseName("IX_ZauberTag_TagName");

                    b.ToTable("ZauberTags", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Tags.Models.TagItem", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DateUpdated")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("ItemId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("TagId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ItemId")
                        .HasDatabaseName("IX_ZauberTagItems_ItemId");

                    b.HasIndex("TagId");

                    b.ToTable("ZauberTagItems", (string)null);
                });

            modelBuilder.Entity("ZauberCMS.Core.Audit.Models.Audit", b =>
                {
                    b.HasOne("ZauberCMS.Core.Content.Models.Content", null)
                        .WithMany("Audits")
                        .HasForeignKey("ContentId");

                    b.HasOne("ZauberCMS.Core.Media.Models.Media", null)
                        .WithMany("Audits")
                        .HasForeignKey("MediaId");

                    b.HasOne("ZauberCMS.Core.Membership.Models.User", null)
                        .WithMany("Audits")
                        .HasForeignKey("UserId");
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.Content", b =>
                {
                    b.HasOne("ZauberCMS.Core.Content.Models.ContentType", "ContentType")
                        .WithMany("LinkedContent")
                        .HasForeignKey("ContentTypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ZauberCMS.Core.Languages.Models.Language", "Language")
                        .WithMany()
                        .HasForeignKey("LanguageId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("ZauberCMS.Core.Membership.Models.User", "LastUpdatedBy")
                        .WithMany()
                        .HasForeignKey("LastUpdatedById")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("ZauberCMS.Core.Content.Models.Content", "Parent")
                        .WithMany("Children")
                        .HasForeignKey("ParentId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("ZauberCMS.Core.Content.Models.UnpublishedContent", "UnpublishedContent")
                        .WithMany()
                        .HasForeignKey("UnpublishedContentId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("ContentType");

                    b.Navigation("Language");

                    b.Navigation("LastUpdatedBy");

                    b.Navigation("Parent");

                    b.Navigation("UnpublishedContent");
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.ContentPropertyValue", b =>
                {
                    b.HasOne("ZauberCMS.Core.Content.Models.Content", "Content")
                        .WithMany("PropertyData")
                        .HasForeignKey("ContentId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Content");
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.ContentType", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.User", "LastUpdatedBy")
                        .WithMany()
                        .HasForeignKey("LastUpdatedById")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("LastUpdatedBy");
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.Domain", b =>
                {
                    b.HasOne("ZauberCMS.Core.Languages.Models.Language", "Language")
                        .WithMany("Domains")
                        .HasForeignKey("LanguageId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Language");
                });

            modelBuilder.Entity("ZauberCMS.Core.Languages.Models.LanguageText", b =>
                {
                    b.HasOne("ZauberCMS.Core.Languages.Models.LanguageDictionary", "LanguageDictionary")
                        .WithMany("Texts")
                        .HasForeignKey("LanguageDictionaryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ZauberCMS.Core.Languages.Models.Language", "Language")
                        .WithMany("LanguageTexts")
                        .HasForeignKey("LanguageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Language");

                    b.Navigation("LanguageDictionary");
                });

            modelBuilder.Entity("ZauberCMS.Core.Media.Models.Media", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.User", "LastUpdatedBy")
                        .WithMany()
                        .HasForeignKey("LastUpdatedById")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("ZauberCMS.Core.Media.Models.Media", "Parent")
                        .WithMany("Children")
                        .HasForeignKey("ParentId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("LastUpdatedBy");

                    b.Navigation("Parent");
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.RoleClaim", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.Role", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserClaim", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserLogin", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserPropertyValue", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.User", "User")
                        .WithMany("PropertyData")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserRole", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.Role", "Role")
                        .WithMany("UserRoles")
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ZauberCMS.Core.Membership.Models.User", "User")
                        .WithMany("UserRoles")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Role");

                    b.Navigation("User");
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.UserToken", b =>
                {
                    b.HasOne("ZauberCMS.Core.Membership.Models.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ZauberCMS.Core.Tags.Models.TagItem", b =>
                {
                    b.HasOne("ZauberCMS.Core.Tags.Models.Tag", "Tag")
                        .WithMany("TagItems")
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Tag");
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.Content", b =>
                {
                    b.Navigation("Audits");

                    b.Navigation("Children");

                    b.Navigation("PropertyData");
                });

            modelBuilder.Entity("ZauberCMS.Core.Content.Models.ContentType", b =>
                {
                    b.Navigation("LinkedContent");
                });

            modelBuilder.Entity("ZauberCMS.Core.Languages.Models.Language", b =>
                {
                    b.Navigation("Domains");

                    b.Navigation("LanguageTexts");
                });

            modelBuilder.Entity("ZauberCMS.Core.Languages.Models.LanguageDictionary", b =>
                {
                    b.Navigation("Texts");
                });

            modelBuilder.Entity("ZauberCMS.Core.Media.Models.Media", b =>
                {
                    b.Navigation("Audits");

                    b.Navigation("Children");
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.Role", b =>
                {
                    b.Navigation("UserRoles");
                });

            modelBuilder.Entity("ZauberCMS.Core.Membership.Models.User", b =>
                {
                    b.Navigation("Audits");

                    b.Navigation("PropertyData");

                    b.Navigation("UserRoles");
                });

            modelBuilder.Entity("ZauberCMS.Core.Tags.Models.Tag", b =>
                {
                    b.Navigation("TagItems");
                });
#pragma warning restore 612, 618
        }
    }
}
