using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Rendering;

public class ZauberPageViewModel(Content.Models.Content content) : IHasPropertyValues
{
    
    public ContentType? ContentType
    {
        get => content.ContentType;
        set => content.ContentType = value;
    }

    public string? ContentTypeAlias
    {
        get => content.ContentTypeAlias;
        set => content.ContentTypeAlias = value;
    }

    public Guid? LastUpdatedById
    {
        get => content.LastUpdatedById;
        set => content.LastUpdatedById = value;
    }

    public User? LastUpdatedBy
    {
        get => content.LastUpdatedBy;
        set => content.LastUpdatedBy = value;
    }
    
    public List<Guid> Path
    {
        get => content.Path;
        set => content.Path = value;
    }

    public bool Published
    {
        get => content.Published;
        set => content.Published = value;
    }

    public bool HideFromNavigation
    {
        get => content.HideFromNavigation;
        set => content.HideFromNavigation = value;
    }

    public Content.Models.Content? Parent
    {
        get => content.Parent;
        set => content.Parent = value;
    }

    /// <summary>
    /// Manually set language of the page
    /// </summary>
    public Guid? LanguageId
    {
        get => content.LanguageId;
        set => content.LanguageId = value;
    }

    /// <summary>
    /// Manually set Language of the page
    /// </summary>
    public Language? Language
    {
        get => content.Language;
        set => content.Language = value;
    }

    public Guid Id
    {
        get => content.Id;
        set => content.Id = value;
    }

    public string? Name
    {
        get => content.Name;
        set => content.Name = value;
    }

    public string? Url
    {
        get => content.Url;
        set => content.Url = value;
    }

    public Guid ContentTypeId
    {
        get => content.ContentTypeId;
        set => content.ContentTypeId = value;
    }

    public int SortOrder
    {
        get => content.SortOrder;
        set => content.SortOrder = value;
    }

    public bool IsRootContent
    {
        get => content.IsRootContent;
        set => content.IsRootContent = value;
    }

    public Guid? InternalRedirectId
    {
        get => content.InternalRedirectId;
        set => content.InternalRedirectId = value;
    }

    public Guid? ParentId
    {
        get => content.ParentId;
        set => content.ParentId = value;
    }

    public DateTime DateCreated
    {
        get => content.DateCreated;
        set => content.DateCreated = value;
    }

    public DateTime DateUpdated
    {
        get => content.DateUpdated;
        set => content.DateUpdated = value;
    }

    public string ViewComponent
    {
        get => content.ViewComponent;
        set => content.ViewComponent = value;
    }
    
    public Dictionary<string, string> ContentValues()
    {
        return content.ContentValues();
    }
}