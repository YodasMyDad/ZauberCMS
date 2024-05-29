using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Core.Sections;

public class MembersSection : ISection
{
    public string Name => "Members";
    public string Alias => "MembersSection";
    public string IndexUrl => "/admin/members";

    public int SortOrder => 3;
    public string DashboardAlias => "MembersDashboards";
    public string NavigationAlias => "MembersNavigation";
}