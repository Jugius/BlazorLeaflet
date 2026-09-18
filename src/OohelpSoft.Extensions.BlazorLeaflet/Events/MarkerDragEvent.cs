using OohelpSoft.BlazorLeaflet.Base.Types;

namespace OohelpSoft.BlazorLeaflet.Events;

public sealed record MarkerDragEvent(string MarkerId, Location Location);
