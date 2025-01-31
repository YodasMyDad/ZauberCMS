namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentBlock
{
    string ContentTypeAlias { get; }
    Models.Content? Content { get; set; }
}