using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Seo.Navigation;

public class SeoNavGroup : ISectionNavGroup
{
    public string Heading => "Seo";
    public string Alias => Constants.Sections.SectionNavGroups.SettingsSeoNavGroup;
    public int SortOrder => 25;
    public string SectionAlias => Constants.Sections.SettingsSection;
}