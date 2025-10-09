using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class CreateContentVersionParameters
{
    public required Models.Content Content { get; set; }
    public ContentVersionStatus Status { get; set; } = ContentVersionStatus.Draft;
    public string? VersionName { get; set; }
    public string? Comments { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsAutoSave { get; set; }
    public Guid? ParentVersionId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public class PublishContentVersionParameters
{
    public required Guid VersionId { get; set; }
    public Guid? PublishedByUserId { get; set; }
}

public class QueryContentVersionsParameters
{
    public required Guid ContentId { get; set; }
    public ContentVersionStatus? Status { get; set; }
    public Guid? CreatedById { get; set; }
    public string? VersionName { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public bool IncludeAutoSaves { get; set; } = false;
    public ContentVersionOrderBy OrderBy { get; set; } = ContentVersionOrderBy.VersionNumber;
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
}

public enum ContentVersionOrderBy
{
    VersionNumber,
    DateCreated,
    DatePublished
}

public class GetContentVersionParameters
{
    public required Guid VersionId { get; set; }
}

public class DeleteContentVersionParameters
{
    public required Guid VersionId { get; set; }
}

public class CompareContentVersionsParameters
{
    public required Guid VersionId1 { get; set; }
    public required Guid VersionId2 { get; set; }
}

public class ContentVersionComparison
{
    public required ContentVersion Version1 { get; set; }
    public required ContentVersion Version2 { get; set; }
    public required List<ContentDifference> Differences { get; set; }
    public required List<PropertyDifference> PropertyDifferences { get; set; }
    public required List<BlockListDifference> BlockListDifferences { get; set; }
}

public class ContentDifference
{
    public required string Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class PropertyDifference
{
    public required string Alias { get; set; }
    public required string OldValue { get; set; }
    public required string NewValue { get; set; }
}

public class BlockListDifference
{
    public required string PropertyAlias { get; set; }
    public required string PropertyName { get; set; }
    public required List<BlockListContentChange> ContentChanges { get; set; }
}

public enum BlockListContentChangeType
{
    Added,
    Removed,
    Modified
}

public class BlockListContentChange
{
    public required BlockListContentChangeType ChangeType { get; set; }
    public required string ContentName { get; set; }
    public required string ContentTypeAlias { get; set; }
    public List<PropertyDifference>? PropertyChanges { get; set; }
    public Guid? ContentId { get; set; }
}
