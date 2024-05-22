using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Models;

public class ListPropertySettingsModel
{
    /// <summary>
    /// The optional data source for the list
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Manual items for the list
    /// </summary>
    public List<DataListItem> Items { get; set; } = [];
    
    /// <summary>
    /// Limit the maximum amount selectable
    /// </summary>
    public int MaxAmount { get; set; }
}