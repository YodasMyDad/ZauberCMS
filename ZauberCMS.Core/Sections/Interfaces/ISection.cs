namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISection
{
    string Name { get; }
    string Alias { get; }
    string IndexUrl { get; }
    int SortOrder { get; }
    string DashboardAlias { get; }
    string NavigationAlias { get; }
}