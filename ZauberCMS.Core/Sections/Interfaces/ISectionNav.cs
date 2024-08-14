namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNav : IDisposable
{
    int SortOrder { get; }
    string SectionNavGroupAlias { get; }
}