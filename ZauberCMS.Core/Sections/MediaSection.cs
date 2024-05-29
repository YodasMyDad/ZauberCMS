using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Core.Sections;

public class MediaSection : ISection
{
    public string Name => "Media";
    public string Alias => "MediaSection";
    public string IndexUrl => "/admin/media";

    public int SortOrder => 1;
    public string DashboardAlias => "MediaDashboards";
    public string NavigationAlias => "MediaNavigation";
}