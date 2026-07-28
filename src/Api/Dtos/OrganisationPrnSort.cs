using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrganisationPrnSort
{
    IssuedAtDescending,
    IssuedAtAscending,
    TonnageDescending,
    TonnageAscending,
    IssuerDescending,
    IssuerAscending,
    DecemberWasteDescending,
    MaterialDescending,
    MaterialAscending,
}
