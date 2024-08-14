using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.UsersSection.Navigation;

public class UsersNavGroup : ISectionNavGroup
{
    public string Heading => "User Management";
    public string Alias => Constants.Sections.SectionNavGroups.UsersNavGroup;
    public int SortOrder => 0;
    public string SectionAlias => Constants.Sections.UsersSection;
}