using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.StructureSection.Navigation;

public class CompositionsNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_compositionsGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.StructureCompositionsNavGroup;
    public int SortOrder => 30;
    public string SectionAlias => Constants.Sections.StructureSection;
}