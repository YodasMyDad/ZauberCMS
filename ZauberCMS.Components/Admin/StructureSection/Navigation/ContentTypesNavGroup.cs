using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.StructureSection.Navigation;

public class ContentTypesNavGroup : ISectionNavGroup
{
    public string Heading => "Content Types";
    public string Alias => Constants.Sections.SectionNavGroups.StructureContentTypesNavGroup;
    public int SortOrder => 10;
    public string SectionAlias => Constants.Sections.StructureSection;
}