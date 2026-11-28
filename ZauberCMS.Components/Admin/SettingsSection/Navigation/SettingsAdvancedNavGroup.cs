using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.SettingsSection.Navigation;

public class SettingsAdvancedNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_advancedGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.SettingsAdvancedNavGroup;
    public int SortOrder => 40;
    public string SectionAlias => Constants.Sections.SettingsSection;
}