using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.SettingsSection.Navigation;

public class SettingsLanguageNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_languagesGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.SettingsLanguagesNavGroup;
    public int SortOrder => 30;
    public string SectionAlias => Constants.Sections.SettingsSection;
}