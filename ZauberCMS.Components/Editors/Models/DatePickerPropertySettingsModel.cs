namespace ZauberCMS.Components.Editors.Models;

public class DatePickerPropertySettingsModel
{
    public bool ShowTimeOnly { get; set; }
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public bool InlineCalendar { get; set; }
}