using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class DataGridContentParameters : BaseQueryContentParameters
{
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
    public string? Order { get; set; }
    public string? Filter { get; set; }
}