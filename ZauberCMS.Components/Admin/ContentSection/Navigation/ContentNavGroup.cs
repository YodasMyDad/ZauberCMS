using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.ContentSection.Navigation;

public class ContentNavGroup : ISectionNavGroup
{
    public string Heading => "Content";
    public string Alias => Constants.Sections.SectionNavGroups.ContentNavGroup;
    public int SortOrder => 0;
    public string SectionAlias => Constants.Sections.ContentSection;

    /// <summary>Never read: a group's "…" actions are the discovered <see cref="ISectionNavGroupAction"/>s (ZA-121).</summary>
    [Obsolete("Never shown. Add a group action by implementing ISectionNavGroupAction. Will be removed in a later major version.")]
    public List<ISectionNavGroupAction> Actions => [];
}