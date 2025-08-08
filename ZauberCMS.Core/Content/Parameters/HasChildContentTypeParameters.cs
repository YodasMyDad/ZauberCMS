using System;

namespace ZauberCMS.Core.Content.Parameters;

public class HasChildContentTypeParameters
{
    public bool Cached { get; set; } = true;
    public Guid ParentId { get; set; }
}