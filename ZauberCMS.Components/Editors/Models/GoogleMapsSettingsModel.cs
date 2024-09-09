namespace ZauberCMS.Components.Editors.Models;

public class GoogleMapsSettingsModel
{
    public double? DefaultLat { get; set; } = 51.5074;
    public double? DefaultLng { get; set; } = 0.1278;
    public int ZoomLevel { get; set; } = 3;
}