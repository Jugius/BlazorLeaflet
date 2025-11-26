using Microsoft.JSInterop;

namespace OohelpSoft.BlazorLeaflet;

public interface IMap
{
    string Id { get; }
    IJSObjectReference Interop {  get; }
}
