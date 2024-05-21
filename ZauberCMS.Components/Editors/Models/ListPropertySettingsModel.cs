using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Models;

public class ListPropertySettingsModel
{
    public string DataSource { get; set; } = string.Empty;

    public List<DataListItem> Items { get; set; } = [];
}