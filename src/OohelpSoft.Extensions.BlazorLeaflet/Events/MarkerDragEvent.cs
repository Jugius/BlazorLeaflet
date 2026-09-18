namespace OohelpSoft.BlazorLeaflet.Events;

public sealed record MarkerDragEvent(string MarkerId, double Latitude, double Longitude);
