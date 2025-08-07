namespace ZauberCMS.Core.Data.Parameters;

public class GetGlobalDataParameters
{
    public string? Alias { get; set; }
    public bool Cached { get; set; } = true;
}