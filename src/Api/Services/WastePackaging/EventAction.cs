using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.WastePackaging;

[JsonConverter(typeof(JsonStringEnumConverter<EventAction>))]
public enum EventAction
{
    Joined,
    Left,
}
