using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.SettingsSection.Navigation;

public class SettingsStructureNavGroup : ISectionNavGroup
{
    public string Heading => "Structure";
    public string Alias => Constants.Sections.SectionNavGroups.SettingsStructureNavGroup;
    public int SortOrder => 20;
    public string SectionAlias => Constants.Sections.SettingsSection;
}