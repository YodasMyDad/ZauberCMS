using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.StructureSection.Navigation;

public class ElementTypesNavGroup : ISectionNavGroup
{
    public string Heading => "Element Types";
    public string Alias => Constants.Sections.SectionNavGroups.StructureElementTypesNavGroup;
    public int SortOrder => 20;
    public string SectionAlias => Constants.Sections.StructureSection;
}