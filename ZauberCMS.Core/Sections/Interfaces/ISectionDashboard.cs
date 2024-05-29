namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionDashboard
{
    string Name { get; }
    int SortOrder { get; }
    string SectionAlias { get; }
}