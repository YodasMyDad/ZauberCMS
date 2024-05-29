using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Core.Sections;

public class ContentSection : ISection
{
    public string Name => "Content";
    public string Alias => "ContentSection";
    public string IndexUrl => "/admin/content";

    public int SortOrder => 0;
    public string DashboardAlias => "ContentDashboards";
    public string NavigationAlias => "ContentNavigation";
}