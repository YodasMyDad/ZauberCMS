namespace ZauberCMS.Core.Content.Interfaces;

public interface ICustomContentComponent
{
    string Name { get; }
    Models.Content? Content { get; set; }
}