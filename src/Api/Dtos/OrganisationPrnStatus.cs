using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrganisationPrnStatus
{
    AwaitingAcceptance,
    Accepted,
    Rejected,
    Cancelled,
}
