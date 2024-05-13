﻿using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class Content
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Url { get; set; }
    public Guid ContentTypeId { get; set; }
    public ContentType ContentType { get; set; } = default!;
    public int SortOrder { get; set; }
    public Guid? ParentId { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public List<ContentValue> ContentPropertyData { get; set; } = [];
}