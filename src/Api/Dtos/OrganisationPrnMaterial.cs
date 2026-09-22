using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrganisationPrnMaterial
{
    Plastic,
    Glass,
    Aluminium,
    Steel,
    Wood,
    GlassRemelt,
    Paper,
}
