using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.SettingsSection.Navigation;

public class SettingsNavGroup(IStringLocalizer<SharedResource> Localizer) : ISectionNavGroup
{
    public string Heading => Localizer["Heading.Settings"];
    public string Alias => Constants.Sections.SectionNavGroups.SettingsNavGroup;
    public int SortOrder => 10;
    public string SectionAlias => Constants.Sections.SettingsSection;
}