using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.MediaSection.Navigation;

public class MediaNavGroup : ISectionNavGroup
{
    public string Heading => "Media";
    public string Alias => Constants.Sections.SectionNavGroups.MediaNavGroup;
    public int SortOrder => 0;
    public string SectionAlias => Constants.Sections.MediaSection;
}