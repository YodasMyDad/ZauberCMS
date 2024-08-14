using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.ContentSection.Navigation;

public class ContentNavGroup : ISectionNavGroup
{
    public string Heading => "Content";
    public string Alias => Constants.Sections.SectionNavGroups.ContentNavGroup;
    public int SortOrder => 0;
    public string SectionAlias => Constants.Sections.ContentSection;
}