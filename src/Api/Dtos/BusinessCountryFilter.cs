using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BusinessCountryFilter
{
    [JsonStringEnumMemberName("GB-ENG")]
    England,

    [JsonStringEnumMemberName("GB-NIR")]
    NorthernIreland,

    [JsonStringEnumMemberName("GB-SCT")]
    Scotland,

    [JsonStringEnumMemberName("GB-WLS")]
    Wales,
}
