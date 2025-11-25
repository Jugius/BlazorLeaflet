namespace OohelpSoft.BlazorLeaflet.Layers.Raster;

public class TileLayer
{
    public required string Name { get; set; }
    public required string TyleUrl { get; set; }
    public string Attribution { get; set; } = string.Empty;
}
