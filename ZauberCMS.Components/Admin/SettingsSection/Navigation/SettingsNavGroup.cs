using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.SettingsSection.Navigation;

public class SettingsNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_settingsGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.SettingsNavGroup;
    public int SortOrder => 10;
    public string SectionAlias => Constants.Sections.SettingsSection;
}