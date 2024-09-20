using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.SettingsSection.Navigation;

public class SettingsAdvancedNavGroup : ISectionNavGroup
{
    public string Heading => "Advanced";
    public string Alias => Constants.Sections.SectionNavGroups.SettingsAdvancedNavGroup;
    public int SortOrder => 40;
    public string SectionAlias => Constants.Sections.SettingsSection;
}