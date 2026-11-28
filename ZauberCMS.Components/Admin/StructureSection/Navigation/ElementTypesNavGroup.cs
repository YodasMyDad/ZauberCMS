using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.StructureSection.Navigation;

public class ElementTypesNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_elementTypesGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.StructureElementTypesNavGroup;
    public int SortOrder => 20;
    public string SectionAlias => Constants.Sections.StructureSection;
}