using System.ComponentModel.DataAnnotations.Schema;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Content.Models;

public class ContentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public Guid ContentId { get; set; }

    /// <summary>
    /// Version number (incremental)
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Version name/label (optional, user-defined)
    /// </summary>
    public string? VersionName { get; set; }

    /// <summary>
    /// Version status
    /// </summary>
    public ContentVersionStatus Status { get; set; } = ContentVersionStatus.Draft;

    /// <summary>
    /// JSON snapshot of the complete content at this version
    /// </summary>
    public ContentSnapshot Snapshot { get; set; } = new();

    /// <summary>
    /// JSON of property values for this version
    /// </summary>
    public List<ContentPropertySnapshot> PropertySnapshots { get; set; } = [];

    /// <summary>
    /// Snapshots of nested content affected by block list changes in this version
    /// </summary>
    public List<BlockListContentSnapshot> BlockListSnapshots { get; set; } = [];

    /// <summary>
    /// User who created this version
    /// </summary>
    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    /// <summary>
    /// When this version was created
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this version was published (if it was)
    /// </summary>
    public DateTime? DatePublished { get; set; }

    /// <summary>
    /// Comments/notes about this version
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// Whether this version is the currently published version
    /// </summary>
    public bool IsCurrentPublished { get; set; }

    /// <summary>
    /// Whether this version is the latest draft
    /// </summary>
    public bool IsLatestDraft { get; set; }

    /// <summary>
    /// Reference to parent version (for branching/merging if needed)
    /// </summary>
    public Guid? ParentVersionId { get; set; }
    public ContentVersion? ParentVersion { get; set; }

    /// <summary>
    /// Version tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Whether this version was auto-saved
    /// </summary>
    public bool IsAutoSave { get; set; }

    /// <summary>
    /// Content size/complexity score for performance optimization
    /// </summary>
    public int ContentSize { get; set; }
}

public enum ContentVersionStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
    Scheduled = 3,
    Rejected = 4
}

public class ContentSnapshot
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public Guid ContentTypeId { get; set; }
    public string? ContentTypeAlias { get; set; }
    public DateTime DateUpdated { get; set; }
    public bool Published { get; set; }
    public bool HideFromNavigation { get; set; }
    public Guid? LanguageId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? RelatedContentId { get; set; }
    public bool IsNestedContent { get; set; }
    public List<Guid> Path { get; set; } = [];
    public int SortOrder { get; set; }
}

public class ContentPropertySnapshot
{
    public Guid ContentTypePropertyId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}

public class BlockListContentSnapshot
{
    /// <summary>
    /// The ID of the nested content item
    /// </summary>
    public Guid ContentId { get; set; }

    /// <summary>
    /// Full snapshot of the nested content at this version
    /// </summary>
    public ContentSnapshot ContentSnapshot { get; set; } = new();

    /// <summary>
    /// Property values of the nested content at this version
    /// </summary>
    public List<ContentPropertySnapshot> PropertySnapshots { get; set; } = [];
}

