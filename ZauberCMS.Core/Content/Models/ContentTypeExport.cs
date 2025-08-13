using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Models;

public class ContentTypePackage
{
    public ContentTypeExport Type { get; set; } = new();
    public List<ContentExport> RootContents { get; set; } = new();
}

public class ContentTypeExport
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Icon { get; set; } = "description";
    public bool IsElementType { get; set; }
    public bool AllowAtRoot { get; set; }
    public bool EnableListView { get; set; }
    public bool IncludeChildren { get; set; }
    public List<string> AvailableContentViews { get; set; } = new();
    public List<string> AllowedChildContentTypeAliases { get; set; } = new();
    public List<Tab> Tabs { get; set; } = new();
    public List<PropertyType> ContentProperties { get; set; } = new();
    public string ParentAlias { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public bool IsComposition { get; set; }
    public List<string> CompositionAliases { get; set; } = new();
    public string MediaIdAsString { get; set; } = string.Empty;
}

public class ContentExport
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentTypeAlias { get; set; } = string.Empty;
    public bool Published { get; set; }
    public bool Deleted { get; set; }
    public bool HideFromNavigation { get; set; }
    public string InternalRedirectIdAsString { get; set; } = string.Empty; // Will need handling on import
    public int SortOrder { get; set; }
    public string ViewComponent { get; set; } = string.Empty;
    public string LanguageIsoCode { get; set; } = string.Empty;
    public Dictionary<string, string> PropertyData { get; set; } = new(); // Key: PropertyType Alias, Value: serialized value
    public List<ContentExport> Children { get; set; } = new();
}
