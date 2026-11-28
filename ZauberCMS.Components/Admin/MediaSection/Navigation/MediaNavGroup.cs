using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.MediaSection.Navigation;

public class MediaNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_mediaGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.MediaNavGroup;
    public int SortOrder => 0;
    public string SectionAlias => Constants.Sections.MediaSection;
}