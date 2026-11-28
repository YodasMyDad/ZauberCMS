using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.StructureSection.Navigation;

public class ContentTypesNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_contentTypesGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.StructureContentTypesNavGroup;
    public int SortOrder => 10;
    public string SectionAlias => Constants.Sections.StructureSection;
}