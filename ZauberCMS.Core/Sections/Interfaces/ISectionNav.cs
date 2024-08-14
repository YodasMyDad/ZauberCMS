namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNav : IDisposable
{
    string Heading { get; }
    string Alias { get; }
    int SortOrder { get; }
    string SectionAlias { get; }
}