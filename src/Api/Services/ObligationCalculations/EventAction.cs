using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

[JsonConverter(typeof(JsonStringEnumConverter<EventAction>))]
public enum EventAction
{
    Joined,
    Left,
}
