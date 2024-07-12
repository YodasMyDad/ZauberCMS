namespace ZauberCMS.Core.Shared.Interfaces;

public interface ITreeItem
{
    Guid Id { get; set; }
    string? Name { get; set; }
}