using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Seo.Navigation;

public class SeoNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_seoGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.SettingsSeoNavGroup;
    public int SortOrder => 25;
    public string SectionAlias => Constants.Sections.SettingsSection;
}