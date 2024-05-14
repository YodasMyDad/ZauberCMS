﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentDbMapping : IEntityTypeConfiguration<Models.Content>
{
    public void Configure(EntityTypeBuilder<Models.Content> builder)
    {
        builder.ToTable("ZauberContent");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(1000);
        builder.Property(x => x.PageTitle).HasMaxLength(1000);
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.DateUpdated).IsRequired();
        builder.Property(x => x.ViewComponent).HasMaxLength(1000);
        builder.Property(e => e.ContentPropertyData).ToJsonConversion(4000);
        
        builder.HasOne(d => d.ContentType)
            .WithMany(p => p.LinkedContent)
            .HasForeignKey(d => d.ContentTypeId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(x => x.Url).HasDatabaseName("IX_ZauberContent_Url");
        builder.HasIndex(x => x.Name).HasDatabaseName("IX_ZauberContent_Name");
    }
}