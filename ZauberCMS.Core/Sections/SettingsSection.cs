using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Core.Sections;

public class SettingsSection : ISection
{
    public string Name => "Settings";
    public string Alias => "SettingsSection";
    public string IndexUrl => "/admin/settings";

    public int SortOrder => 2;
    public string DashboardAlias => "SettingsDashboards";
    public string NavigationAlias => "SettingsNavigation";
}