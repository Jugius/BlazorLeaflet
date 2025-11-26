using Microsoft.JSInterop;

namespace OohelpSoft.BlazorLeaflet.Base.Interfaces;

public interface IMap
{
    string Id { get; }
    IJSObjectReference Interop {  get; }
}
