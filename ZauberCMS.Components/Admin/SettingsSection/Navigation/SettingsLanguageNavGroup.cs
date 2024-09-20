using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.SettingsSection.Navigation;

public class SettingsLanguageNavGroup : ISectionNavGroup
{
    public string Heading => "Languages";
    public string Alias => Constants.Sections.SectionNavGroups.SettingsLanguagesNavGroup;
    public int SortOrder => 30;
    public string SectionAlias => Constants.Sections.SettingsSection;
}