namespace ZauberCMS.Components.Editors.Models;

public class NumericPropertySettingsModel
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public string? Step { get; set; }
    public string? Placeholder { get; set; }
    public string? Format { get; set; }
    public bool AlignRight { get; set; }
}