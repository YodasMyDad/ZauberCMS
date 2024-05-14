namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentView
{
    /// <summary>
    /// The Content item that the View uses
    /// </summary>
    Models.Content? Content { get; set; }
}