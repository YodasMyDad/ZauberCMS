using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.StructureSection.Navigation;

public class CompositionsNavGroup : ISectionNavGroup
{
    public string Heading => "Compositions";
    public string Alias => Constants.Sections.SectionNavGroups.StructureCompositionsNavGroup;
    public int SortOrder => 30;
    public string SectionAlias => Constants.Sections.StructureSection;
}