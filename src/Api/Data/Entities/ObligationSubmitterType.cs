using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Data.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<ObligationSubmitterType>))]
public enum ObligationSubmitterType
{
    DirectRegistrant,
    ComplianceScheme,
}
