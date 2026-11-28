using Microsoft.Extensions.Localization;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.Admin.UsersSection.Navigation;

public class UsersNavGroup(IStringLocalizer<ZauberText> text) : ISectionNavGroup
{
    public string Heading => text["sections_usersGroup"];
    public string Alias => Constants.Sections.SectionNavGroups.UsersNavGroup;
    public int SortOrder => 0;
    public string SectionAlias => Constants.Sections.UsersSection;
}